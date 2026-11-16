using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Encuadre
{
    public sealed class SnapResult
    {
        public ActionId Action;
        public Monitor Monitor;
        public SnapDir Dir;
        public RECT Rect;
    }

    // Arrastrar ventanas a los bordes/esquinas, como las "snap areas" de Rectangle
    public sealed class SnapManager : IDisposable
    {
        readonly App app;
        readonly Timer timer = new Timer();
        readonly Footprint footprint = new Footprint();
        IntPtr win;
        RECT startFrame;
        bool wasZoomed, dragging, down;
        int hit;
        POINT downPt;
        SnapResult current;
        ActionId prior = ActionId.None;

        IntPtr pendingWin;
        ActionId pendingAction = ActionId.None;
        RECT pendingStart;
        POINT pendingCursor;
        bool pendingUnsnap;
        readonly Timer pendingTimer = new Timer();

        public SnapManager(App app)
        {
            this.app = app;
            timer.Interval = 15;
            timer.Tick += Tick;
            pendingTimer.Interval = 250;
            pendingTimer.Tick += delegate { RunPending(); };
            var h = footprint.Handle; // crear la ventana ya
        }

        public void Dispose() { timer.Dispose(); pendingTimer.Dispose(); footprint.Dispose(); }

        static int S(Monitor m, int v)
        {
            // márgenes de Rectangle: 5 px de borde, 20 px de esquina, 145 px de lado corto
            double scale = 1.0;
            try
            {
                uint dx, dy;
                if (GetDpiForMonitor(m.Handle, 0, out dx, out dy) == 0) scale = dx / 96.0;
            }
            catch { }
            return (int)Math.Round(v * scale);
        }

        [System.Runtime.InteropServices.DllImport("shcore.dll")]
        static extern int GetDpiForMonitor(IntPtr hmon, int type, out uint dpiX, out uint dpiY);

        public static SnapResult Compute(Config cfg, int x, int y, ActionId prior)
        {
            var m = Monitors.FromPoint(x, y);
            if (m == null) return null;
            RECT f = m.Bounds;
            int margin = S(m, 5), corner = S(m, 20), shortEdge = S(m, 145);
            bool atL = x <= f.Left + margin, atR = x >= f.Right - 1 - margin;
            bool atT = y <= f.Top + margin, atB = y >= f.Bottom - 1 - margin;
            if (!atL && !atR && !atT && !atB) return null;
            bool nearL = x <= f.Left + corner, nearR = x >= f.Right - 1 - corner;
            bool nearT = y <= f.Top + corner, nearB = y >= f.Bottom - 1 - corner;

            SnapDir d;
            if ((atL && nearT) || (atT && nearL)) d = SnapDir.TL;
            else if ((atR && nearT) || (atT && nearR)) d = SnapDir.TR;
            else if ((atL && nearB) || (atB && nearL)) d = SnapDir.BL;
            else if ((atR && nearB) || (atB && nearR)) d = SnapDir.BR;
            else if (atT) d = SnapDir.T;
            else if (atB) d = SnapDir.B;
            else if (atL) d = SnapDir.L;
            else d = SnapDir.R;

            string choice;
            if (!cfg.SnapAreas.TryGetValue(d, out choice)) choice = SnapChoices.Default(d);
            ActionId a = ActionId.None;
            switch (choice)
            {
                case SnapChoices.None: return null;
                case SnapChoices.LeftCompound:
                case SnapChoices.RightCompound:
                {
                    bool left = choice == SnapChoices.LeftCompound;
                    if (y <= f.Top + margin + shortEdge) a = ActionId.TopHalf;
                    else if (y >= f.Bottom - margin - shortEdge) a = ActionId.BottomHalf;
                    else a = left ? ActionId.LeftHalf : ActionId.RightHalf;
                    break;
                }
                case SnapChoices.ThirdsCompound:
                {
                    bool horizontal = d == SnapDir.B || d == SnapDir.T;
                    double pos = horizontal ? (x - f.Left) / (double)f.Width : (y - f.Top) / (double)f.Height;
                    if (pos < 1.0 / 3)
                        a = (prior == ActionId.CenterThird || prior == ActionId.FirstTwoThirds) ? ActionId.FirstTwoThirds : ActionId.FirstThird;
                    else if (pos > 2.0 / 3)
                        a = (prior == ActionId.CenterThird || prior == ActionId.LastTwoThirds) ? ActionId.LastTwoThirds : ActionId.LastThird;
                    else if (prior == ActionId.FirstThird || prior == ActionId.FirstTwoThirds) a = ActionId.FirstTwoThirds;
                    else if (prior == ActionId.LastThird || prior == ActionId.LastTwoThirds) a = ActionId.LastTwoThirds;
                    else a = ActionId.CenterThird;
                    break;
                }
                default:
                    if (!Enum.TryParse(choice, out a)) return null;
                    break;
            }
            var r = new SnapResult { Action = a, Monitor = m, Dir = d };
            r.Rect = Executor.TileFor(a, 0, m, cfg, Math.Max(0, cfg.Gap));
            return r;
        }

        public void LeftDown(int x, int y)
        {
            down = true;
            dragging = false;
            current = null;
            prior = ActionId.None;
            win = IntPtr.Zero;
            var cfg = app.Config;
            if (!cfg.SnapEnabled) return;
            IntPtr h = Win.Root(Native.WindowFromPoint(new POINT(x, y)));
            if (!Win.IsManageable(h) || app.IsIgnored(h)) return;
            IntPtr res;
            hit = 0;
            if (Native.SendMessageTimeout(h, Native.WM_NCHITTEST, IntPtr.Zero, (IntPtr)((y << 16) | (x & 0xFFFF)), Native.SMTO_ABORTIFHUNG, 60, out res) != IntPtr.Zero)
                hit = res.ToInt32();
            if (hit >= 10 && hit <= 17) return; // borde de redimensionar
            win = h;
            startFrame = Native.GetFrame(h);
            wasZoomed = Native.IsZoomed(h);
            downPt = new POINT(x, y);
            timer.Start();
        }

        void Tick(object sender, EventArgs e)
        {
            try
            {
                if (win == IntPtr.Zero || !down) { timer.Stop(); return; }
                if (!Native.KeyDown(0x01) && !Native.KeyDown(0x02)) { LeftUp(downPt.X, downPt.Y); return; }
                POINT c;
                Native.GetCursorPos(out c);
                if (!dragging)
                {
                    RECT f = Native.GetFrame(win);
                    bool moved = f.Left != startFrame.Left || f.Top != startFrame.Top;
                    bool sameSize = f.Width == startFrame.Width && f.Height == startFrame.Height;
                    bool cursorMoved = Math.Abs(c.X - downPt.X) > 3 || Math.Abs(c.Y - downPt.Y) > 3;
                    bool captionLike = hit == Native.HTCAPTION || wasZoomed;
                    if (moved && cursorMoved && (sameSize || captionLike)) dragging = true;
                    else if (!sameSize && !captionLike) { win = IntPtr.Zero; timer.Stop(); return; }
                    if (!dragging) return;
                }
                var r = Compute(app.Config, c.X, c.Y, prior);
                if (r == null)
                {
                    if (current != null) footprint.HideNow();
                    current = null;
                    return;
                }
                if (current == null || r.Action != current.Action || r.Monitor.Handle != current.Monitor.Handle)
                {
                    bool fresh = current == null;
                    current = r;
                    prior = r.Action;
                    footprint.ShowAt(r.Rect, fresh && app.Config.AnimateFootprint);
                }
            }
            catch (Exception ex) { Log.Write("Snap.Tick", ex); }
        }

        public void LeftUp(int x, int y)
        {
            down = false;
            timer.Stop();
            footprint.HideNow();
            if (win == IntPtr.Zero) return;
            IntPtr h = win;
            win = IntPtr.Zero;
            if (!dragging) return;
            POINT c;
            Native.GetCursorPos(out c);
            if (current != null)
            {
                pendingWin = h; pendingAction = current.Action; pendingStart = startFrame; pendingUnsnap = false;
            }
            else if (app.Config.RestoreOnUnsnap && !wasZoomed)
            {
                pendingWin = h; pendingAction = ActionId.None; pendingStart = startFrame; pendingUnsnap = true; pendingCursor = c;
            }
            else return;
            current = null;
            pendingTimer.Stop();
            pendingTimer.Start();
        }

        // Se llama cuando Windows avisa de que terminó el arrastre (o por tiempo)
        public void MoveSizeEnded(IntPtr h)
        {
            if (h == pendingWin) RunPending();
        }

        void RunPending()
        {
            pendingTimer.Stop();
            IntPtr h = pendingWin;
            pendingWin = IntPtr.Zero;
            if (h == IntPtr.Zero) return;
            try
            {
                if (pendingUnsnap) app.Exec.UnsnapRestore(h, pendingStart, pendingCursor);
                else app.Exec.Run(pendingAction, h, true, pendingStart);
            }
            catch (Exception ex) { Log.Write("Snap.RunPending", ex); }
        }
    }

    // Vista previa translúcida con esquinas redondeadas (la "huella" de Rectangle)
    public sealed class Footprint : Form
    {
        readonly Timer fade = new Timer();
        byte alpha = 255;
        RECT rect;
        Bitmap bmp;
        bool shown;

        public Footprint()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            fade.Interval = 15;
            fade.Tick += delegate
            {
                int a = alpha + 40;
                alpha = (byte)Math.Min(255, a);
                Push();
                if (alpha >= 255) fade.Stop();
            };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        public void ShowAt(RECT r, bool animate)
        {
            rect = r;
            if (bmp != null) bmp.Dispose();
            var mon = Monitors.FromPoint(r.Left + r.Width / 2, r.Top + r.Height / 2);
            float scale = 1f;
            if (mon != null)
            {
                uint dx, dy;
                try { if (GetDpiForMonitor(mon.Handle, 0, out dx, out dy) == 0) scale = dx / 96f; } catch { }
            }
            bmp = Render(r.Width, r.Height, scale);
            if (animate) { alpha = 0; fade.Start(); } else { fade.Stop(); alpha = 255; }
            Push();
            if (!shown)
            {
                Native.ShowWindow(Handle, Native.SW_SHOWNOACTIVATE);
                shown = true;
            }
            Native.SetWindowPos(Handle, Native.HWND_TOPMOST, r.Left, r.Top, r.Width, r.Height, Native.SWP_NOACTIVATE);
        }

        public void HideNow()
        {
            fade.Stop();
            if (shown) { Native.ShowWindow(Handle, Native.SW_HIDE); shown = false; }
            if (bmp != null) { bmp.Dispose(); bmp = null; }
        }

        [System.Runtime.InteropServices.DllImport("shcore.dll")]
        static extern int GetDpiForMonitor(IntPtr hmon, int type, out uint dpiX, out uint dpiY);

        static Bitmap Render(int w, int h, float scale)
        {
            var b = new Bitmap(Math.Max(1, w), Math.Max(1, h), PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                float rad = 10 * scale;
                using (var path = Theme.Rounded(new RectangleF(1.5f, 1.5f, w - 3f, h - 3f), rad))
                {
                    using (var fill = new SolidBrush(Color.FromArgb(78, 150, 150, 156))) g.FillPath(fill, path);
                    using (var outer = new Pen(Color.FromArgb(70, 0, 0, 0), 3f)) g.DrawPath(outer, path);
                    using (var inner = new Pen(Color.FromArgb(150, 255, 255, 255), 1.5f)) g.DrawPath(inner, path);
                }
            }
            return b;
        }

        void Push()
        {
            if (bmp == null) return;
            IntPtr screen = Native.GetDC(IntPtr.Zero);
            IntPtr mem = Native.CreateCompatibleDC(screen);
            IntPtr hbmp = bmp.GetHbitmap(Color.FromArgb(0));
            IntPtr old = Native.SelectObject(mem, hbmp);
            try
            {
                var size = new SIZE(bmp.Width, bmp.Height);
                var src = new POINT(0, 0);
                var dst = new POINT(rect.Left, rect.Top);
                var blend = new BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = alpha, AlphaFormat = 1 };
                Native.UpdateLayeredWindow(Handle, screen, ref dst, ref size, mem, ref src, 0, ref blend, Native.ULW_ALPHA);
            }
            finally
            {
                Native.SelectObject(mem, old);
                Native.DeleteObject(hbmp);
                Native.DeleteDC(mem);
                Native.ReleaseDC(IntPtr.Zero, screen);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { fade.Dispose(); if (bmp != null) bmp.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
