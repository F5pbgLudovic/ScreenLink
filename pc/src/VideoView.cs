using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenLinkPC
{
    /// <summary>Affiche l'écran du téléphone et traduit la souris en touchers.</summary>
    sealed unsafe class VideoView : Control
    {
        Bitmap bmp;
        readonly object lk = new object();
        public int VW, VH;
        public bool ShowFinger = true;
        public bool Bt709 = true;
        bool down;
        PointF fingerPos;

        public Action<int, float, float> Touch;            // 0 appui, 1 déplacement, 2 relâchement
        public Action<float, float, int, int, bool> Wheel;
        public Action<int> Key;

        public VideoView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            TabStop = true;
            BackColor = Color.Black;
        }

        public Rectangle Dest()
        {
            if (VW <= 0 || VH <= 0) return ClientRectangle;
            float s = Math.Min(ClientSize.Width / (float)VW, ClientSize.Height / (float)VH);
            int w = (int)(VW * s), h = (int)(VH * s);
            return new Rectangle((ClientSize.Width - w) / 2, (ClientSize.Height - h) / 2, w, h);
        }

        public void Reset(int w, int h)
        {
            lock (lk)
            {
                VW = w; VH = h;
                if (bmp != null) { bmp.Dispose(); bmp = null; }
                bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
            }
        }

        /// <summary>Convertit une image NV12 en pixels et demande l'affichage.</summary>
        public void Present(IntPtr nv12, int stride, int w, int h)
        {
            lock (lk)
            {
                if (bmp == null || bmp.Width > w || bmp.Height > h) return;
                int cw = bmp.Width, ch = bmp.Height;
                BitmapData bd = bmp.LockBits(new Rectangle(0, 0, cw, ch), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
                IntPtr src = nv12, dst = bd.Scan0;
                int dstStride = bd.Stride, uvOff = stride * h;
                bool b709 = Bt709;
                Parallel.For(0, ch / 2, delegate(int row)
                {
                    byte* y0 = (byte*)src.ToPointer() + (row * 2) * stride;
                    byte* y1 = y0 + stride;
                    byte* uv = (byte*)src.ToPointer() + uvOff + row * stride;
                    byte* d0 = (byte*)dst.ToPointer() + (row * 2) * dstStride;
                    byte* d1 = d0 + dstStride;
                    int cr = b709 ? 459 : 409, cgu = b709 ? 55 : 100, cgv = b709 ? 136 : 208, cb = b709 ? 541 : 516;
                    for (int x = 0; x < cw; x += 2)
                    {
                        int u = uv[x] - 128, v = uv[x + 1] - 128;
                        int rr = cr * v, gg = cgu * u + cgv * v, bb = cb * u;
                        for (int k = 0; k < 2; k++)
                        {
                            int a = 298 * (y0[x + k] - 16) + 128;
                            int r = (a + rr) >> 8, g = (a - gg) >> 8, b = (a + bb) >> 8;
                            byte* o = d0 + (x + k) * 4;
                            o[0] = (byte)(b < 0 ? 0 : b > 255 ? 255 : b); o[1] = (byte)(g < 0 ? 0 : g > 255 ? 255 : g); o[2] = (byte)(r < 0 ? 0 : r > 255 ? 255 : r); o[3] = 255;
                            a = 298 * (y1[x + k] - 16) + 128;
                            r = (a + rr) >> 8; g = (a - gg) >> 8; b = (a + bb) >> 8;
                            o = d1 + (x + k) * 4;
                            o[0] = (byte)(b < 0 ? 0 : b > 255 ? 255 : b); o[1] = (byte)(g < 0 ? 0 : g > 255 ? 255 : g); o[2] = (byte)(r < 0 ? 0 : r > 255 ? 255 : r); o[3] = 255;
                        }
                    }
                });
                bmp.UnlockBits(bd);
            }
            if (IsHandleCreated) { try { BeginInvoke((MethodInvoker)delegate { Invalidate(); }); } catch { } }
        }

        public Bitmap Snapshot()
        {
            lock (lk) { return bmp == null ? null : (Bitmap)bmp.Clone(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.Black);
            lock (lk)
            {
                if (bmp == null) return;
                g.InterpolationMode = InterpolationMode.Bilinear;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.CompositingQuality = CompositingQuality.HighSpeed;
                Rectangle d = Dest();
                g.DrawImage(bmp, d);
            }
            if (ShowFinger && down)
            {
                int r = Math.Max(12, Dest().Width / 22);
                using (Brush b = new SolidBrush(Color.FromArgb(90, 255, 255, 255)))
                    g.FillEllipse(b, fingerPos.X - r, fingerPos.Y - r, r * 2, r * 2);
                using (Pen p = new Pen(Color.FromArgb(200, 255, 255, 255), 2))
                    g.DrawEllipse(p, fingerPos.X - r, fingerPos.Y - r, r * 2, r * 2);
            }
        }

        bool Norm(Point p, bool clamp, out float x, out float y)
        {
            Rectangle d = Dest();
            x = (p.X - d.X) / (float)Math.Max(1, d.Width);
            y = (p.Y - d.Y) / (float)Math.Max(1, d.Height);
            bool inside = x >= 0 && x <= 1 && y >= 0 && y <= 1;
            if (clamp) { x = Math.Max(0, Math.Min(1, x)); y = Math.Max(0, Math.Min(1, y)); }
            return inside;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            if (VW == 0) return;
            if (e.Button == MouseButtons.Left)
            {
                float x, y;
                if (!Norm(e.Location, false, out x, out y)) return;
                down = true; fingerPos = e.Location;
                if (Touch != null) Touch(0, x, y);
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!down) return;
            float x, y;
            Norm(e.Location, true, out x, out y);
            fingerPos = e.Location;
            if (Touch != null) Touch(1, x, y);
            if (ShowFinger) Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (VW == 0) return;
            if (e.Button == MouseButtons.Left && down)
            {
                float x, y;
                Norm(e.Location, true, out x, out y);
                down = false;
                if (Touch != null) Touch(2, x, y);
                Invalidate();
            }
            else if (e.Button == MouseButtons.Right) { if (Key != null) Key(Link.KEY_BACK); }
            else if (e.Button == MouseButtons.Middle) { if (Key != null) Key(Link.KEY_HOME); }
            else if (e.Button == MouseButtons.XButton1) { if (Key != null) Key(Link.KEY_BACK); }
            else if (e.Button == MouseButtons.XButton2) { if (Key != null) Key(Link.KEY_RECENTS); }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (VW == 0) return;
            float x, y;
            Norm(e.Location, true, out x, out y);
            int n = e.Delta / 120;
            if (n == 0) n = e.Delta > 0 ? 1 : -1;
            bool shift = (ModifierKeys & Keys.Shift) != 0, ctrl = (ModifierKeys & Keys.Control) != 0;
            if (Wheel != null) Wheel(x, y, shift ? n : 0, shift ? 0 : n, ctrl);
        }

        protected override bool IsInputKey(Keys k) { return true; }
    }
}
