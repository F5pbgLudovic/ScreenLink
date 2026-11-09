using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ScreenLinkPC
{
    sealed class MainForm : Form
    {
        readonly Settings st = Settings.Load();
        readonly VideoView view = new VideoView();
        readonly Panel top = new Panel();
        readonly StatusStrip strip = new StatusStrip();
        readonly ToolStripStatusLabel lbl = new ToolStripStatusLabel();
        ComboBox cbMode, cbSize, cbFps, cbRate, cbDev;
        TextBox tbHost, tbPin;
        CheckBox chkAudio, chkFinger, chkMute, chkMuteTel, chkNoNotif;
        bool dndOk = true;
        Button btnConnect;

        Link link;
        MfDecoder dec;
        AudioOut aout;
        readonly object decLock = new object();
        volatile bool connecting;
        int frames, fpsShown;
        long fpsT = Stopwatch.GetTimestamp();
        string phoneName = "", extra = "";
        bool inputOk = true;
        int curW, curH;
        bool loading = true;

        public MainForm()
        {
            Text = "ScreenLink V1.2 @F5PBG 2026";
            Icon = SystemIcons.Application;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            BackColor = Color.FromArgb(24, 28, 36);
            ForeColor = Color.White;
            KeyPreview = true;
            DoubleBuffered = true;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(480, 900);

            BuildTop();
            strip.Items.Add(lbl);
            strip.BackColor = Color.FromArgb(24, 28, 36);
            lbl.ForeColor = Color.White;
            view.Dock = DockStyle.Fill;
            Controls.Add(view);
            Controls.Add(strip);
            Controls.Add(top);
            view.BringToFront();

            view.Touch = delegate(int a, float x, float y) { if (link != null) link.Touch(a, x, y); };
            view.Wheel = delegate(float x, float y, int dx, int dy, bool c) { if (link != null) link.Scroll(x, y, dx, dy, c); };
            view.Key = delegate(int k) { if (link != null) link.Key(k); };

            Rectangle saved = new Rectangle(st.X, st.Y, Math.Max(st.W, 1), Math.Max(st.H, 1));
            bool visibleSaved = false;
            foreach (Screen sc in Screen.AllScreens) { Rectangle i = Rectangle.Intersect(sc.WorkingArea, saved); if (i.Width > 100 && i.Height > 100) visibleSaved = true; }
            if (st.W > 200 && st.H > 200 && visibleSaved)
            {
                Location = new Point(st.X, st.Y);
                Size = new Size(st.W, st.H);
            }
            else CenterToScreen();
            loading = false;
            SetStatus(L.T("Non connecté", "Not connected"));
            Shown += delegate { if (st.Host.Length == 0 && st.Mode == "wifi") tbHost.Focus(); if (Array.IndexOf(Environment.GetCommandLineArgs(), "/connect") > 0) Connect(); };
            MinimumSize = new Size(430, 500);
            FormClosing += delegate { SaveSettings(); Disconnect(); };
        }

        // ------------------------------------------------------------------ interface
        Label Lab(string t) { Label l = new Label(); l.Text = t; l.AutoSize = true; l.Margin = new Padding(8, 8, 2, 0); l.ForeColor = Color.FromArgb(200, 210, 230); return l; }

        ComboBox Combo(int w, params string[] items)
        {
            ComboBox c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList; c.Width = w; c.Margin = new Padding(2, 4, 2, 0);
            c.Items.AddRange(items);
            return c;
        }

        Button Btn(string t, int w, EventHandler h)
        {
            Button b = new Button();
            b.Text = t; b.Width = w; b.Height = 26; b.Margin = new Padding(2, 3, 2, 0);
            b.FlatStyle = FlatStyle.System; b.TabStop = false;
            b.Click += delegate(object s, EventArgs e) { h(s, e); view.Focus(); };
            return b;
        }

        void BuildTop()
        {
            FlowLayoutPanel r1 = new FlowLayoutPanel(), r2 = new FlowLayoutPanel(), r3 = new FlowLayoutPanel();
            foreach (FlowLayoutPanel r in new FlowLayoutPanel[] { r1, r2, r3 })
            { r.Dock = DockStyle.Top; r.AutoSize = true; r.WrapContents = true; r.AutoSizeMode = AutoSizeMode.GrowAndShrink; r.Padding = new Padding(2, 0, 2, 2); }

            cbMode = Combo(70, "Wi-Fi", "USB");
            cbMode.SelectedIndex = st.Mode == "usb" ? 1 : 0;
            cbMode.SelectedIndexChanged += delegate { tbHost.Enabled = cbMode.SelectedIndex == 0; };
            tbHost = new TextBox { Width = 110, Text = st.Host, Margin = new Padding(2, 4, 2, 0) };
            tbHost.Enabled = cbMode.SelectedIndex == 0;
            tbPin = new TextBox { Width = 60, Text = st.Pin, MaxLength = 6, Margin = new Padding(2, 4, 2, 0) };
            btnConnect = Btn(L.T("Connecter", "Connect"), 90, delegate { if (link != null || connecting) Disconnect(); else Connect(); });
            r1.Controls.AddRange(new Control[] { Lab(L.T("Liaison", "Link")), cbMode, Lab("IP"), tbHost, Lab(L.T("Code", "Code")), tbPin, btnConnect });

            cbSize = Combo(78, "1280", "1600", "1920", "2400", L.T("Originale", "Native"));
            int[] sizes = { 1280, 1600, 1920, 2400, 4096 };
            cbSize.SelectedIndex = Math.Max(0, Array.IndexOf(sizes, st.MaxSize));
            if (Array.IndexOf(sizes, st.MaxSize) < 0) cbSize.SelectedIndex = 2;
            cbFps = Combo(50, "24", "30", "60");
            cbFps.SelectedIndex = st.Fps >= 60 ? 2 : st.Fps >= 30 ? 1 : 0;
            cbRate = Combo(64, "2", "4", "8", "12", "20", "30");
            int[] rates = { 2000, 4000, 8000, 12000, 20000, 30000 };
            cbRate.SelectedIndex = Math.Max(0, Array.IndexOf(rates, st.Kbps)); if (Array.IndexOf(rates, st.Kbps) < 0) cbRate.SelectedIndex = 2;
            chkAudio = new CheckBox { Text = L.T("Son", "Audio"), Checked = st.Audio, AutoSize = true, Margin = new Padding(8, 6, 2, 0) };
            chkMute = new CheckBox { Text = L.T("Muet", "Mute"), AutoSize = true, Margin = new Padding(4, 6, 2, 0) };
            chkMute.CheckedChanged += delegate { if (aout != null) aout.Muted = chkMute.Checked; };
            chkMuteTel = new CheckBox { Text = "Mute tel", Checked = st.MuteTel, AutoSize = true, Margin = new Padding(4, 6, 2, 0) };
            chkNoNotif = new CheckBox { Text = L.T("Pas de notif", "No notif"), Checked = st.NoNotif, AutoSize = true, Margin = new Padding(4, 6, 2, 0) };
            cbDev = Combo(170);
            cbDev.Items.Add(L.T("Sortie son par défaut", "Default audio output"));
            foreach (string d in AudioOut.Devices()) cbDev.Items.Add(d);
            cbDev.SelectedIndex = (st.AudioDev >= 0 && st.AudioDev + 1 < cbDev.Items.Count) ? st.AudioDev + 1 : 0;
            chkFinger = new CheckBox { Text = L.T("Montrer le doigt", "Show finger"), Checked = st.ShowFinger, AutoSize = true, Margin = new Padding(8, 6, 2, 0) };
            chkFinger.CheckedChanged += delegate { view.ShowFinger = chkFinger.Checked; };
            view.ShowFinger = st.ShowFinger;
            r2.Controls.AddRange(new Control[] { Lab(L.T("Taille max", "Max size")), cbSize, Lab("fps"), cbFps, Lab("Mbit/s"), cbRate, chkAudio, chkMute, chkMuteTel, chkNoNotif, cbDev, chkFinger });
            EventHandler apply = delegate { ApplySettings(); };
            cbSize.SelectedIndexChanged += apply; cbFps.SelectedIndexChanged += apply; cbRate.SelectedIndexChanged += apply; chkAudio.CheckedChanged += apply; chkMuteTel.CheckedChanged += apply; chkNoNotif.CheckedChanged += apply;

            r3.Controls.AddRange(new Control[] {
                Btn("◀ " + L.T("Retour", "Back"), 80, delegate { K(Link.KEY_BACK); }),
                Btn("● " + L.T("Accueil", "Home"), 80, delegate { K(Link.KEY_HOME); }),
                Btn("■ " + L.T("Récents", "Recents"), 80, delegate { K(Link.KEY_RECENTS); }),
                Btn(L.T("Notifs", "Notifs"), 58, delegate { K(Link.KEY_NOTIF); }),
                Btn(L.T("Réglages", "Quick"), 64, delegate { K(Link.KEY_QUICK); }),
                Btn("Vol +", 50, delegate { K(Link.KEY_VOLUP); }),
                Btn("Vol −", 50, delegate { K(Link.KEY_VOLDOWN); }),
                Btn(L.T("Réveil", "Wake"), 60, delegate { K(Link.KEY_WAKE); }),
                Btn(L.T("Verrou", "Lock"), 60, delegate { K(Link.KEY_LOCK); }),
                Btn("PNG", 44, delegate { SavePng(); }),
                Btn(L.T("Barre : F10", "Bars: F10"), 88, delegate { ToggleBars(); })
            });
            top.Dock = DockStyle.Top; top.AutoSize = true; top.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            top.Controls.Add(r3); top.Controls.Add(r2); top.Controls.Add(r1);
        }

        void K(int k) { if (link != null) link.Key(k); }

        void ToggleBars()
        {
            bool show = !top.Visible;
            int vh = Math.Max(200, ClientSize.Height - Chrome());
            top.Visible = show; strip.Visible = show;
            if (!show) Text = "ScreenLink V1.2 @F5PBG 2026";
            PerformLayout();
            FitWindow(vh, false);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F10) { ToggleBars(); e.Handled = true; e.SuppressKeyPress = true; return; }
            if (view.Focused && link != null)
            {
                if (e.KeyCode == Keys.Back) { link.Key(Link.KEY_BACKSPACE); e.Handled = true; e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Enter) { link.Key(Link.KEY_ENTER); e.Handled = true; e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Escape) { link.Key(Link.KEY_BACK); e.Handled = true; e.SuppressKeyPress = true; }
                else if (e.Control && e.KeyCode == Keys.V && Clipboard.ContainsText()) { link.Text(Clipboard.GetText()); e.Handled = true; e.SuppressKeyPress = true; }
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (view.Focused && link != null && e.KeyChar >= ' ') { link.Text(e.KeyChar.ToString()); e.Handled = true; }
            base.OnKeyPress(e);
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        /// <summary>Les rangées de boutons (AutoSize + ancrées en haut) ne recalculent pas leur hauteur quand la largeur change : on la fixe.</summary>
        void FixRows()
        {
            int cw = ClientSize.Width;
            if (cw <= 0) return;
            foreach (Control c in top.Controls)
            {
                int h = c.GetPreferredSize(new Size(cw, 0)).Height;
                if (c.AutoSize) c.AutoSize = false;
                if (c.Height != h) c.Height = h;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (top.Controls.Count > 0) FixRows();
        }

        int Chrome() { return (top.Visible ? top.Height : 0) + (strip.Visible ? strip.Height : 0); }

        /// <summary>Hauteur des barres pour une largeur donnée (les boutons passent à la ligne quand la fenêtre est étroite).</summary>
        int ChromeFor(int cw)
        {
            int h = 0;
            if (top.Visible) foreach (Control c in top.Controls) h += c.GetPreferredSize(new Size(cw, 0)).Height;
            if (strip.Visible) h += strip.Height;
            return h;
        }

        /// <summary>Pendant qu'on tire sur un bord de la fenêtre, garde les proportions du téléphone.</summary>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x214 && curW > 0 && curH > 0 && WindowState == FormWindowState.Normal)
            {
                RECT r = (RECT)System.Runtime.InteropServices.Marshal.PtrToStructure(m.LParam, typeof(RECT));
                int nonW = Width - ClientSize.Width, nonH0 = Height - ClientSize.Height, nonH = nonH0 + Chrome();
                double ratio = curW / (double)curH;
                int edge = m.WParam.ToInt32();
                int cw = r.Right - r.Left - nonW, ch = r.Bottom - r.Top - nonH;
                if (edge == 3 || edge == 6) r.Right = r.Left + (int)Math.Round(ch * ratio) + nonW;
                else
                {
                    int nh = (int)Math.Round(cw / ratio) + nonH0 + ChromeFor(cw);
                    if (edge == 4 || edge == 5) r.Top = r.Bottom - nh; else r.Bottom = r.Top + nh;
                }
                System.Runtime.InteropServices.Marshal.StructureToPtr(r, m.LParam, false);
            }
            base.WndProc(ref m);
        }

        /// <summary>Dimensionne la fenêtre aux proportions du téléphone, sans dépasser 90 % de l'écran.</summary>
        void FitWindow(int wantVideoH, bool center)
        {
            if (curW <= 0 || curH <= 0 || WindowState != FormWindowState.Normal) return;
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            int nonW = Width - ClientSize.Width, nonH = Height - ClientSize.Height;
            double ratio = curW / (double)curH;
            int minCW = Math.Max(1, MinimumSize.Width - nonW);
            int maxCW = Math.Max(minCW, (int)(wa.Width * 0.9) - nonW);
            // la hauteur des barres dépend de la largeur (les boutons passent à la ligne) : on itère
            int vh = Math.Max(200, wantVideoH), vw = vh, chrome = 0;
            for (int it = 0; it < 4; it++)
            {
                vw = (int)Math.Round(vh * ratio);
                if (vw > maxCW) vw = maxCW;
                if (vw < minCW) vw = minCW;
                chrome = ChromeFor(vw);
                int maxVH = (int)(wa.Height * 0.9) - nonH - chrome;
                int nh = Math.Min((int)Math.Round(vw / ratio), maxVH);
                if (nh < 200) nh = 200;
                if (nh == vh) break;
                vh = nh;
            }
            ClientSize = new Size(vw, vh + chrome);
            PerformLayout();
            top.PerformLayout();
            int real = Chrome();
            if (real != chrome) { ClientSize = new Size(vw, vh + real); PerformLayout(); }
            int x = center ? wa.Left + (wa.Width - Width) / 2 : Left, y = center ? wa.Top + (wa.Height - Height) / 2 : Top;
            x = Math.Max(wa.Left, Math.Min(x, wa.Right - Width));
            y = Math.Max(wa.Top, Math.Min(y, wa.Bottom - Height));
            Location = new Point(x, y);
            Invalidate(true);
        }

        void KeepAspect() { FitWindow(Math.Max(200, ClientSize.Height - Chrome()), false); }
        void FitFirst() { FitWindow(curH, true); }

        void SavePng()
        {
            Bitmap b = view.Snapshot();
            if (b == null) return;
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            string f = Path.Combine(dir, "ScreenLink-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
            b.Save(f, ImageFormat.Png); b.Dispose();
            SetStatus(L.T("Image enregistrée : ", "Saved: ") + f);
        }

        // ------------------------------------------------------------------ état
        void SetStatus(string s)
        {
            if (InvokeRequired) { try { BeginInvoke((MethodInvoker)delegate { SetStatus(s); }); } catch { } return; }
            lbl.Text = s;
            if (!top.Visible) return;
            Text = "ScreenLink V1.2 @F5PBG 2026 — " + s;
        }

        void SaveSettings()
        {
            st.Mode = cbMode.SelectedIndex == 1 ? "usb" : "wifi";
            st.Host = tbHost.Text.Trim(); st.Pin = tbPin.Text.Trim();
            int[] sizes = { 1280, 1600, 1920, 2400, 4096 };
            st.MaxSize = sizes[cbSize.SelectedIndex];
            st.Fps = new int[] { 24, 30, 60 }[cbFps.SelectedIndex];
            st.Kbps = new int[] { 2000, 4000, 8000, 12000, 20000, 30000 }[cbRate.SelectedIndex];
            st.Audio = chkAudio.Checked; st.ShowFinger = chkFinger.Checked; st.MuteTel = chkMuteTel.Checked; st.NoNotif = chkNoNotif.Checked;
            st.AudioDev = cbDev.SelectedIndex - 1;
            if (WindowState == FormWindowState.Normal && !loading) { st.X = Left; st.Y = Top; st.W = Width; st.H = Height; }
            st.Save();
        }

        void ApplySettings()
        {
            if (loading) return;
            SaveSettings();
            if (link != null) link.SendSettings(st);
        }

        // ------------------------------------------------------------------ connexion
        void Connect()
        {
            SaveSettings();
            if (st.Pin.Length < 6) { MessageBox.Show(this, L.T("Saisissez le code à 6 chiffres affiché sur le téléphone.", "Enter the 6-digit code shown on the phone."), "ScreenLink"); return; }
            bool usb = st.Mode == "usb";
            if (!usb && st.Host.Length == 0) { MessageBox.Show(this, L.T("Saisissez l'adresse IP affichée sur le téléphone.", "Enter the IP address shown on the phone."), "ScreenLink"); return; }
            connecting = true;
            btnConnect.Text = L.T("Annuler", "Cancel");
            Thread t = new Thread(delegate() { ConnectWork(usb); }) { IsBackground = true };
            t.Start();
        }

        void ConnectWork(bool usb)
        {
            string host = st.Host;
            int port = st.Port;
            string err = null;
            try
            {
                if (usb)
                {
                    string adb = Adb.Find(st.AdbPath);
                    if (adb == null) { Fail(L.T("adb.exe introuvable : placez-le à côté de ScreenLinkPC.exe ou dans le PATH.", "adb.exe not found: put it next to ScreenLinkPC.exe or in PATH.")); return; }
                    SetStatus(L.T("Recherche du téléphone USB…", "Looking for the USB phone…"));
                    string dv = Adb.Run(adb, "devices", 15000);
                    string serial = null;
                    foreach (string line in dv.Split('\n'))
                    {
                        string[] p = line.Trim().Split('\t');
                        if (p.Length == 2 && p[1] == "device") { serial = p[0]; break; }
                        if (p.Length == 2 && p[1] == "unauthorized") { Fail(L.T("Acceptez le débogage USB sur le téléphone, puis recommencez.", "Accept USB debugging on the phone, then try again.")); return; }
                    }
                    if (serial == null) { Fail(L.T("Aucun téléphone USB trouvé (débogage USB activé ?).", "No USB phone found (USB debugging enabled?).")); return; }
                    string s = "-s " + serial + " ";
                    Adb.Run(adb, s + "forward tcp:" + port + " tcp:" + port, 15000);
                    host = "127.0.0.1";
                    Link probe = new Link();
                    err = probe.Connect(host, port, st.Pin, st);
                    if (err == null) { Attach(probe); return; }
                    if (err == L.T("Code incorrect", "Wrong code")) { Fail(err); return; }
                    // le partage n'est pas démarré : on lance l'application sur le téléphone
                    SetStatus(L.T("Démarrage sur le téléphone — acceptez la capture d'écran…", "Starting on the phone — accept the screen capture…"));
                    Adb.Run(adb, s + "shell am start -n fr.f5pbg.screenlink/.MainActivity --ez start true", 15000);
                    DateTime end = DateTime.Now.AddSeconds(60);
                    while (connecting && DateTime.Now < end)
                    {
                        Thread.Sleep(1200);
                        Adb.Run(adb, s + "forward tcp:" + port + " tcp:" + port, 15000);
                        Link l2 = new Link();
                        err = l2.Connect(host, port, st.Pin, st);
                        if (err == null) { Attach(l2); return; }
                        if (err == L.T("Code incorrect", "Wrong code")) { Fail(err); return; }
                    }
                    if (connecting) Fail(L.T("Le téléphone n'a pas démarré le partage.", "The phone did not start sharing."));
                    return;
                }
                SetStatus(L.T("Connexion à ", "Connecting to ") + host + "…");
                Link l = new Link();
                err = l.Connect(host, port, st.Pin, st);
                if (err == null) Attach(l); else Fail(err);
            }
            catch (Exception e) { Fail(e.Message); }
        }

        void Fail(string msg)
        {
            connecting = false;
            SetStatus(msg);
            try { BeginInvoke((MethodInvoker)delegate { btnConnect.Text = L.T("Connecter", "Connect"); }); } catch { }
        }

        void Attach(Link l)
        {
            if (!connecting) { l.Close(null); return; }
            l.VideoConfig = OnVideoConfig;
            l.VideoFrame = OnVideoFrame;
            l.AudioConfig = OnAudioConfig;
            l.Audio = delegate(byte[] pcm) { AudioOut a = aout; if (a != null) a.Push(pcm); };
            l.Info = OnInfo;
            l.Closed = delegate(string why)
            {
                try { BeginInvoke((MethodInvoker)delegate { if (link == l) { Disconnect(); if (why != null) SetStatus(why); } }); } catch { }
            };
            StartDecoder();
            link = l;
            l.Start();
            connecting = false;
            try { BeginInvoke((MethodInvoker)delegate { btnConnect.Text = L.T("Déconnecter", "Disconnect"); view.Focus(); }); } catch { }
            SetStatus(L.T("Connecté — en attente de l'image…", "Connected — waiting for video…"));
        }

        void Disconnect()
        {
            connecting = false;
            Link l = link;
            link = null;
            if (l != null) l.Close(null);
            StopDecoder();
            if (aout != null) { aout.Dispose(); aout = null; }
            if (btnConnect != null) btnConnect.Text = L.T("Connecter", "Connect");
            SetStatus(L.T("Non connecté", "Not connected"));
        }

        void OnInfo(string t)
        {
            foreach (string line in t.Split('\n'))
            {
                int i = line.IndexOf('=');
                if (i < 0) continue;
                string k = line.Substring(0, i), v = line.Substring(i + 1).Trim();
                if (k == "name") phoneName = v;
                else if (k == "input") inputOk = v == "1";
                else if (k == "dnd") dndOk = v == "1";
                else if (k == "error") SetStatus(L.T("Erreur côté téléphone : ", "Phone error: ") + v);
            }
            if (!inputOk) extra = L.T("  ⚠ commande non activée sur le téléphone", "  ⚠ control not enabled on the phone"); else extra = "";
            if (!dndOk) extra += L.T("  ⚠ « Ne pas déranger » non autorisé sur le téléphone", "  ⚠ “Do not disturb” not allowed on the phone");
            ShowRunning();
        }

        void ShowRunning()
        {
            SetStatus(phoneName + "  " + curW + "×" + curH + "  " + fpsShown + " fps" + extra);
        }

        sealed class Job { public int W, H, Off, Len; public byte[] D; public bool Key, Config; }
        readonly Queue<Job> jobs = new Queue<Job>();
        readonly AutoResetEvent jobEv = new AutoResetEvent(false);
        Thread decThread;
        volatile bool decStop;
        bool needKey;

        void StartDecoder()
        {
            StopDecoder();
            lock (jobs) { jobs.Clear(); needKey = false; }
            decStop = false;
            decThread = new Thread(DecodeLoop) { IsBackground = true, Name = "SL-decode" };
            decThread.Start();
        }

        void StopDecoder()
        {
            decStop = true;
            jobEv.Set();
            Thread t = decThread;
            decThread = null;
            if (t != null && t != Thread.CurrentThread) t.Join(1500);
        }

        void DecodeLoop()
        {
            MfDecoder d = null;
            while (!decStop)
            {
                jobEv.WaitOne(200);
                while (!decStop)
                {
                    Job j;
                    lock (jobs) { if (jobs.Count == 0) break; j = jobs.Dequeue(); }
                    try
                    {
                        if (j.Config)
                        {
                            if (d != null) d.Dispose();
                            d = null;
                            d = new MfDecoder(j.W, j.H);
                            d.Frame = delegate(IntPtr p, int stride, int fw, int fh) { view.Present(p, stride, fw, fh); frames++; };
                            view.Reset(j.W, j.H);
                            d.Decode(j.D, 0, j.D.Length);
                        }
                        else if (d != null) d.Decode(j.D, j.Off, j.Len);
                    }
                    catch (Exception e) { Log.W("Decodeur : " + e); SetStatus(L.T("Décodeur H.264 : ", "H.264 decoder: ") + e.Message); if (d != null) { try { d.Dispose(); } catch { } d = null; } }
                    long now = Stopwatch.GetTimestamp();
                    if (now - fpsT >= Stopwatch.Frequency) { fpsShown = frames; frames = 0; fpsT = now; ShowRunning(); }
                }
            }
            if (d != null) d.Dispose();
        }

        void OnVideoConfig(int w, int h, byte[] csd)
        {
            lock (jobs)
            {
                jobs.Clear();
                jobs.Enqueue(new Job { Config = true, W = w, H = h, D = csd });
                needKey = false;
            }
            jobEv.Set();
            bool first = curW == 0;
            curW = w; curH = h;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (first) FitFirst(); else KeepAspect();
                    Invalidate();
                    // la mise en page des barres (boutons à la ligne) ne se termine qu'après le redimensionnement : on ajuste une 2e fois
                    System.Windows.Forms.Timer tm = new System.Windows.Forms.Timer { Interval = 250 };
                    tm.Tick += delegate { tm.Stop(); tm.Dispose(); if (first) FitFirst(); else KeepAspect(); };
                    tm.Start();
                });
            }
            catch { }
        }

        /// <summary>Thread de réception : ne fait que mettre l'image en file (le son n'est jamais retardé par le décodage).</summary>
        void OnVideoFrame(byte[] d, int off, int len, bool key)
        {
            bool askKey = false;
            lock (jobs)
            {
                if (jobs.Count > 6)
                {
                    Queue<Job> keep = new Queue<Job>();
                    foreach (Job x in jobs) if (x.Config) keep.Enqueue(x);
                    jobs.Clear();
                    foreach (Job x in keep) jobs.Enqueue(x);
                    needKey = true; askKey = true;
                }
                if (needKey && !key) return;
                if (key) needKey = false;
                jobs.Enqueue(new Job { D = d, Off = off, Len = len, Key = key });
            }
            jobEv.Set();
            if (askKey) { Link l = link; if (l != null) l.RequestKeyFrame(); }
        }

        void OnAudioConfig(int rate, int ch)
        {
            if (aout != null) { aout.Dispose(); aout = null; }
            try
            {
                aout = new AudioOut(rate, ch, cbDev.SelectedIndex - 1);
                aout.Muted = chkMute.Checked;
            }
            catch (Exception e) { Log.W("Audio : " + e); SetStatus(L.T("Sortie son : ", "Audio output: ") + e.Message); }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
