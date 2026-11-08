using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace ScreenLinkPC
{
    /// <summary>Connexion au téléphone : authentification, réception image/son, envoi des commandes.</summary>
    sealed class Link
    {
        public const int V_CONFIG = 1, V_FRAME = 2, A_CONFIG = 3, A_DATA = 4, INFO = 5;
        public const int C_TOUCH = 10, C_SCROLL = 11, C_KEY = 12, C_TEXT = 13, C_SETTINGS = 14, C_KEYFRAME = 15, C_PING = 16;

        public const int KEY_BACK = 1, KEY_HOME = 2, KEY_RECENTS = 3, KEY_NOTIF = 4, KEY_VOLUP = 5, KEY_VOLDOWN = 6,
            KEY_WAKE = 7, KEY_QUICK = 8, KEY_ENTER = 9, KEY_BACKSPACE = 10, KEY_LOCK = 11, KEY_POWER = 12;

        public Action<int, int, byte[]> VideoConfig;          // largeur, hauteur, SPS/PPS
        public Action<byte[], int, int, bool> VideoFrame;     // données, offset, longueur, image clé
        public Action<int, int> AudioConfig;                   // fréquence, canaux
        public Action<byte[]> Audio;
        public Action<string> Info;
        public Action<string> Closed;

        TcpClient tcp;
        NetworkStream ns;
        readonly object sendLock = new object();
        volatile bool closed;
        Thread reader, pinger;

        public bool IsOpen { get { return tcp != null && !closed; } }

        /// <summary>Retourne null si tout va bien, sinon le message d'erreur.</summary>
        public string Connect(string host, int port, string pin, Settings st)
        {
            try
            {
                tcp = new TcpClient();
                tcp.NoDelay = true;
                IAsyncResult ar = tcp.BeginConnect(host, port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(4000)) { tcp.Close(); return L.T("Délai dépassé (téléphone injoignable ?)", "Timed out (phone unreachable?)"); }
                tcp.EndConnect(ar);
                ns = tcp.GetStream();
                ns.ReadTimeout = 6000;
                byte[] hdr = ReadN(21);
                if (hdr[0] != 'S' || hdr[1] != 'L' || hdr[2] != 'N' || hdr[3] != 'K') { tcp.Close(); return L.T("Ce n'est pas ScreenLink", "Not a ScreenLink phone"); }
                byte[] nonce = new byte[16];
                Array.Copy(hdr, 5, nonce, 0, 16);
                byte[] pb = Encoding.UTF8.GetBytes(pin);
                byte[] all = new byte[16 + pb.Length];
                Array.Copy(nonce, all, 16);
                Array.Copy(pb, 0, all, 16, pb.Length);
                byte[] h = SHA256.Create().ComputeHash(all);
                ns.Write(h, 0, h.Length);
                int r = ns.ReadByte();
                if (r == 1) { tcp.Close(); return L.T("Code incorrect", "Wrong code"); }
                if (r == 2) { tcp.Close(); return L.T("Le téléphone est déjà connecté à un PC", "The phone is already connected to a PC"); }
                if (r == 3) { tcp.Close(); return L.T("Trop d'essais : patientez une minute", "Too many attempts: wait a minute"); }
                if (r != 0) { tcp.Close(); return L.T("Le téléphone a fermé la connexion (le partage est-il démarré ?)", "The phone closed the connection (is sharing started?)"); }
                SendSettings(st);
                ns.ReadTimeout = 8000;
                reader = new Thread(ReadLoop) { IsBackground = true, Name = "SL-read" };
                reader.Start();
                pinger = new Thread(PingLoop) { IsBackground = true, Name = "SL-ping" };
                pinger.Start();
                return null;
            }
            catch (Exception e)
            {
                try { tcp.Close(); } catch { }
                tcp = null;
                if (e is EndOfStreamException || e is IOException) return L.T("Le téléphone a fermé la connexion (le partage est-il démarré ?)", "The phone closed the connection (is sharing started?)");
                return e.Message;
            }
        }

        byte[] ReadN(int n)
        {
            byte[] b = new byte[n];
            int o = 0;
            while (o < n)
            {
                int r = ns.Read(b, o, n - o);
                if (r <= 0) throw new EndOfStreamException();
                o += r;
            }
            return b;
        }

        void ReadLoop()
        {
            string why = null;
            try
            {
                while (!closed)
                {
                    byte[] h = ReadN(5);
                    int type = h[0];
                    int len = (h[1] << 24) | (h[2] << 16) | (h[3] << 8) | h[4];
                    if (len < 0 || len > (64 << 20)) throw new IOException("trame invalide");
                    byte[] p = ReadN(len);
                    switch (type)
                    {
                        case V_CONFIG:
                            if (VideoConfig != null)
                            {
                                int w = (p[0] << 8) | p[1], hh = (p[2] << 8) | p[3];
                                byte[] c = new byte[p.Length - 4];
                                Array.Copy(p, 4, c, 0, c.Length);
                                VideoConfig(w, hh, c);
                            }
                            break;
                        case V_FRAME:
                            if (VideoFrame != null && p.Length > 9) VideoFrame(p, 9, p.Length - 9, p[8] != 0);
                            break;
                        case A_CONFIG:
                            if (AudioConfig != null) AudioConfig((p[0] << 24) | (p[1] << 16) | (p[2] << 8) | p[3], p[4]);
                            break;
                        case A_DATA:
                            if (Audio != null) Audio(p);
                            break;
                        case INFO:
                            if (Info != null) Info(Encoding.UTF8.GetString(p));
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                if (!closed) why = (e is IOException || e is EndOfStreamException) ? L.T("Connexion perdue", "Connection lost") : e.Message;
            }
            Close(why);
        }

        void PingLoop()
        {
            while (!closed)
            {
                Thread.Sleep(2000);
                Send(C_PING, new byte[0]);
            }
        }

        public void Close(string why)
        {
            if (closed) return;
            closed = true;
            try { tcp.Close(); } catch { }
            if (Closed != null) Closed(why);
        }

        public void Send(int type, byte[] payload)
        {
            if (closed || ns == null) return;
            byte[] pkt = new byte[5 + payload.Length];
            pkt[0] = (byte)type;
            pkt[1] = (byte)(payload.Length >> 24); pkt[2] = (byte)(payload.Length >> 16);
            pkt[3] = (byte)(payload.Length >> 8); pkt[4] = (byte)payload.Length;
            Array.Copy(payload, 0, pkt, 5, payload.Length);
            try { lock (sendLock) ns.Write(pkt, 0, pkt.Length); }
            catch { Close(L.T("Connexion perdue", "Connection lost")); }
        }

        static void PutI(byte[] b, int o, int v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }
        static void PutF(byte[] b, int o, float f)
        {
            byte[] x = BitConverter.GetBytes(f);
            b[o] = x[3]; b[o + 1] = x[2]; b[o + 2] = x[1]; b[o + 3] = x[0];
        }

        public void SendSettings(Settings st)
        {
            byte[] b = new byte[8];
            b[0] = (byte)(st.MaxSize >> 8); b[1] = (byte)st.MaxSize;
            b[2] = (byte)st.Fps;
            PutI(b, 3, st.Kbps);
            b[7] = (byte)(st.Audio ? 1 : 0);
            Send(C_SETTINGS, b);
        }

        public void Touch(int action, float x, float y)
        {
            byte[] b = new byte[9];
            b[0] = (byte)action; PutF(b, 1, x); PutF(b, 5, y);
            Send(C_TOUCH, b);
        }

        public void Scroll(float x, float y, int dx, int dy, bool ctrl)
        {
            byte[] b = new byte[13];
            PutF(b, 0, x); PutF(b, 4, y);
            b[8] = (byte)(dx >> 8); b[9] = (byte)dx; b[10] = (byte)(dy >> 8); b[11] = (byte)dy;
            b[12] = (byte)(ctrl ? 1 : 0);
            Send(C_SCROLL, b);
        }

        public void Key(int k) { Send(C_KEY, new byte[] { (byte)k }); }
        public void Text(string s) { Send(C_TEXT, Encoding.UTF8.GetBytes(s)); }
        public void RequestKeyFrame() { Send(C_KEYFRAME, new byte[0]); }
    }
}
