using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace ScreenLinkPC
{
    /// <summary>Lecture du son du téléphone (PCM 16 bits) avec WinMM, sur le périphérique choisi.</summary>
    sealed class AudioOut : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        struct WAVEFORMATEX { public ushort wFormatTag, nChannels; public uint nSamplesPerSec, nAvgBytesPerSec; public ushort nBlockAlign, wBitsPerSample, cbSize; }

        [StructLayout(LayoutKind.Sequential)]
        struct WAVEHDR { public IntPtr lpData; public uint dwBufferLength, dwBytesRecorded; public IntPtr dwUser; public uint dwFlags, dwLoops; public IntPtr lpNext, reserved; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct WAVEOUTCAPS
        {
            public ushort wMid, wPid; public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szPname;
            public uint dwFormats; public ushort wChannels, wReserved1; public uint dwSupport;
        }

        [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr h, int dev, ref WAVEFORMATEX f, IntPtr cb, IntPtr inst, uint flags);
        [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] static extern int waveOutUnprepareHeader(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] static extern int waveOutReset(IntPtr h);
        [DllImport("winmm.dll")] static extern int waveOutClose(IntPtr h);
        [DllImport("winmm.dll")] static extern int waveOutGetNumDevs();
        [DllImport("winmm.dll", CharSet = CharSet.Auto)] static extern int waveOutGetDevCaps(IntPtr dev, ref WAVEOUTCAPS caps, int size);

        public static List<string> Devices()
        {
            List<string> l = new List<string>();
            int n = waveOutGetNumDevs();
            for (int i = 0; i < n; i++)
            {
                WAVEOUTCAPS c = new WAVEOUTCAPS();
                if (waveOutGetDevCaps((IntPtr)i, ref c, Marshal.SizeOf(typeof(WAVEOUTCAPS))) == 0) l.Add(c.szPname); else l.Add("#" + i);
            }
            return l;
        }

        [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint ms);
        const int NBUF = 16;
        readonly int chunk;
        IntPtr hwo;
        readonly IntPtr[] hdrs = new IntPtr[NBUF];
        readonly IntPtr[] data = new IntPtr[NBUF];
        readonly Queue<byte[]> queue = new Queue<byte[]>();
        int queued, firstOff;
        readonly object lk = new object();
        Thread th;
        volatile bool stop;
        bool primed;
        int primeNeed = 8;                        // 160 ms au départ, 80 ms après une panne sèche
        readonly int bytesPerSec;
        public volatile bool Muted;

        public AudioOut(int rate, int channels, int device)
        {
            WAVEFORMATEX f = new WAVEFORMATEX();
            f.wFormatTag = 1; f.nChannels = (ushort)channels; f.nSamplesPerSec = (uint)rate; f.wBitsPerSample = 16;
            f.nBlockAlign = (ushort)(channels * 2); f.nAvgBytesPerSec = (uint)(rate * channels * 2);
            bytesPerSec = (int)f.nAvgBytesPerSec;
            chunk = bytesPerSec / 50;                 // 20 ms
            int r = waveOutOpen(out hwo, device < 0 ? -1 : device, ref f, IntPtr.Zero, IntPtr.Zero, 0);
            if (r != 0) throw new Exception("waveOutOpen " + r);
            for (int i = 0; i < NBUF; i++)
            {
                data[i] = Marshal.AllocHGlobal(chunk);
                hdrs[i] = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WAVEHDR)));
                WAVEHDR h = new WAVEHDR();
                h.lpData = data[i]; h.dwBufferLength = (uint)chunk;
                Marshal.StructureToPtr(h, hdrs[i], false);
                waveOutPrepareHeader(hwo, hdrs[i], Marshal.SizeOf(typeof(WAVEHDR)));
                // marque « terminé » pour qu'il soit libre au départ
                int fo = IntPtr.Size * 2 + 8; Marshal.WriteInt32(hdrs[i], fo, Marshal.ReadInt32(hdrs[i], fo) | 1);
            }
            timeBeginPeriod(1);
            th = new Thread(Pump) { IsBackground = true, Name = "SL-audio-out", Priority = ThreadPriority.AboveNormal };
            th.Start();
        }

        public void Push(byte[] pcm)
        {
            lock (lk)
            {
                queue.Enqueue(pcm);
                queued += pcm.Length;
                // retard excessif (plus de 300 ms) : on rattrape en jetant les plus anciens
                while (queued - firstOff > bytesPerSec * 45 / 100 && queue.Count > 1)
                {
                    byte[] d = queue.Dequeue();
                    queued -= d.Length;
                    firstOff = 0;
                }
            }
        }

        bool Take(IntPtr dst)
        {
            lock (lk)
            {
                if (queued - firstOff < chunk) return false;
                int need = chunk, o = 0;
                byte[] tmp = new byte[chunk];
                while (need > 0)
                {
                    byte[] cur = queue.Peek();
                    int avail = cur.Length - firstOff;
                    int n = Math.Min(avail, need);
                    Buffer.BlockCopy(cur, firstOff, tmp, o, n);
                    o += n; need -= n; firstOff += n;
                    if (firstOff >= cur.Length) { queue.Dequeue(); queued -= cur.Length; firstOff = 0; }
                }
                if (Muted) Array.Clear(tmp, 0, tmp.Length);
                Marshal.Copy(tmp, 0, dst, chunk);
                return true;
            }
        }

        void Pump()
        {
            int hs = Marshal.SizeOf(typeof(WAVEHDR));
            int flagsOff = IntPtr.Size * 2 + 8;
            while (!stop)
            {
                // attend 60 ms de son avant de (re)démarrer pour absorber les irrégularités du réseau
                if (!primed)
                {
                    lock (lk) { if (queued - firstOff >= chunk * primeNeed) primed = true; }
                    if (!primed) { Thread.Sleep(5); continue; }
                }
                bool wrote = false, allDone = true;
                for (int i = 0; i < NBUF; i++)
                {
                    uint fl = (uint)Marshal.ReadInt32(hdrs[i], flagsOff);
                    if ((fl & 1) == 0) { allDone = false; continue; }      // WHDR_DONE absent : en cours de lecture
                    if (Take(data[i]))
                    {
                        Marshal.WriteInt32(hdrs[i], flagsOff, (int)(fl & ~1u));
                        waveOutWrite(hwo, hdrs[i], hs);
                        wrote = true; allDone = false;
                    }
                }
                if (allDone && !wrote) { primed = false; primeNeed = 4; }                      // panne sèche : on refait le tampon
                Thread.Sleep(2);
            }
        }

        public void Dispose()
        {
            stop = true;
            try { if (th != null) th.Join(500); } catch { }
            if (hwo != IntPtr.Zero)
            {
                waveOutReset(hwo);
                int hs = Marshal.SizeOf(typeof(WAVEHDR));
                for (int i = 0; i < NBUF; i++)
                {
                    waveOutUnprepareHeader(hwo, hdrs[i], hs);
                    Marshal.FreeHGlobal(hdrs[i]); Marshal.FreeHGlobal(data[i]);
                }
                waveOutClose(hwo);
                timeEndPeriod(1);
                hwo = IntPtr.Zero;
            }
        }
    }
}
