using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace ScreenLinkPC
{
    static class L
    {
        public static readonly bool Fr = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr";
        public static string T(string fr, string en) { return Fr ? fr : en; }
    }

    sealed class Settings
    {
        public string Mode = "wifi";          // wifi | usb
        public string Host = "";
        public string Pin = "";
        public int MaxSize = 1920;
        public int Fps = 30;
        public int Kbps = 8000;
        public bool Audio = true;
        public int AudioDev = -1;             // -1 = périphérique par défaut
        public bool ShowFinger = true;
        public bool MuteTel = false;
        public bool NoNotif = false;
        public int Port = 27183;
        public string AdbPath = "";
        public int X = -1, Y = -1, W = 0, H = 0;

        static string FilePath
        {
            get
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenLinkPC");
                Directory.CreateDirectory(d);
                return Path.Combine(d, "settings.ini");
            }
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    int i = line.IndexOf('=');
                    if (i < 0) continue;
                    string k = line.Substring(0, i).Trim(), v = line.Substring(i + 1).Trim();
                    int n;
                    switch (k)
                    {
                        case "Mode": s.Mode = v; break;
                        case "Host": s.Host = v; break;
                        case "Pin": s.Pin = v; break;
                        case "AdbPath": s.AdbPath = v; break;
                        case "MaxSize": if (int.TryParse(v, out n)) s.MaxSize = n; break;
                        case "Fps": if (int.TryParse(v, out n)) s.Fps = n; break;
                        case "Kbps": if (int.TryParse(v, out n)) s.Kbps = n; break;
                        case "Audio": s.Audio = v == "1"; break;
                        case "AudioDev": if (int.TryParse(v, out n)) s.AudioDev = n; break;
                        case "ShowFinger": s.ShowFinger = v == "1"; break;
                        case "MuteTel": s.MuteTel = v == "1"; break;
                        case "NoNotif": s.NoNotif = v == "1"; break;
                        case "Port": if (int.TryParse(v, out n)) s.Port = n; break;
                        case "X": if (int.TryParse(v, out n)) s.X = n; break;
                        case "Y": if (int.TryParse(v, out n)) s.Y = n; break;
                        case "W": if (int.TryParse(v, out n)) s.W = n; break;
                        case "H": if (int.TryParse(v, out n)) s.H = n; break;
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                List<string> l = new List<string>();
                l.Add("Mode=" + Mode); l.Add("Host=" + Host); l.Add("Pin=" + Pin); l.Add("AdbPath=" + AdbPath);
                l.Add("MaxSize=" + MaxSize); l.Add("Fps=" + Fps); l.Add("Kbps=" + Kbps);
                l.Add("Audio=" + (Audio ? "1" : "0")); l.Add("AudioDev=" + AudioDev);
                l.Add("ShowFinger=" + (ShowFinger ? "1" : "0")); l.Add("MuteTel=" + (MuteTel ? "1" : "0")); l.Add("NoNotif=" + (NoNotif ? "1" : "0")); l.Add("Port=" + Port);
                l.Add("X=" + X); l.Add("Y=" + Y); l.Add("W=" + W); l.Add("H=" + H);
                File.WriteAllLines(FilePath, l.ToArray(), new UTF8Encoding(false));
            }
            catch { }
        }
    }

    static class Adb
    {
        public static string Find(string preferred)
        {
            List<string> c = new List<string>();
            if (!string.IsNullOrEmpty(preferred)) c.Add(preferred);
            c.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "adb.exe"));
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string d in path.Split(';'))
                if (d.Trim().Length > 0) { try { c.Add(Path.Combine(d.Trim(), "adb.exe")); } catch { } }
            c.Add(@"C:\Android\adb.exe");
            c.Add(@"C:\platform-tools\adb.exe");
            c.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Android\Sdk\platform-tools\adb.exe"));
            c.Add(@"C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe");
            foreach (string f in c) if (File.Exists(f)) return f;
            return null;
        }

        public static string Run(string adb, string args, int timeoutMs)
        {
            ProcessStartInfo psi = new ProcessStartInfo(adb, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            using (Process p = Process.Start(psi))
            {
                string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } }
                return o;
            }
        }
    }
}

namespace ScreenLinkPC
{
    /// <summary>Journal %APPDATA%\ScreenLinkPC\log.txt (connexions, déconnexions, erreurs) pour comprendre une coupure.</summary>
    static class Log
    {
        static readonly object lk = new object();
        public static void W(string s)
        {
            try
            {
                string d = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenLinkPC");
                System.IO.Directory.CreateDirectory(d);
                string f = System.IO.Path.Combine(d, "log.txt");
                lock (lk)
                {
                    if (System.IO.File.Exists(f) && new System.IO.FileInfo(f).Length > 200000) System.IO.File.Delete(f);
                    System.IO.File.AppendAllText(f, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + s + "\r\n");
                }
            }
            catch { }
        }
    }
}
