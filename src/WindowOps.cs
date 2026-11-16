using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Encuadre
{
    public sealed class Monitor
    {
        public IntPtr Handle;
        public RECT Bounds;
        public RECT Work;
        public bool Portrait { get { return Work.Height > Work.Width; } }
    }

    public static class Monitors
    {
        public static List<Monitor> All()
        {
            var list = new List<Monitor>();
            MonitorEnumProc cb = delegate(IntPtr h, IntPtr dc, ref RECT rc, IntPtr d)
            {
                var m = Info(h);
                if (m != null) list.Add(m);
                return true;
            };
            Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, cb, IntPtr.Zero);
            GC.KeepAlive(cb);
            list.Sort(delegate(Monitor a, Monitor b)
            {
                int c = a.Bounds.Left.CompareTo(b.Bounds.Left);
                return c != 0 ? c : a.Bounds.Top.CompareTo(b.Bounds.Top);
            });
            return list;
        }

        public static Monitor Info(IntPtr h)
        {
            var mi = new MONITORINFO();
            mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            if (!Native.GetMonitorInfo(h, ref mi)) return null;
            return new Monitor { Handle = h, Bounds = mi.rcMonitor, Work = mi.rcWork };
        }

        public static Monitor FromWindow(IntPtr hwnd)
        {
            return Info(Native.MonitorFromWindow(hwnd, Native.MONITOR_DEFAULTTONEAREST));
        }

        public static Monitor FromPoint(int x, int y)
        {
            return Info(Native.MonitorFromPoint(new POINT(x, y), Native.MONITOR_DEFAULTTONEAREST));
        }
    }

    public static class Win
    {
        public static bool AllowOwnProcess = false;
        static readonly uint OwnPid = (uint)Process.GetCurrentProcess().Id;
        static readonly Dictionary<IntPtr, string> exeCache = new Dictionary<IntPtr, string>();

        static readonly string[] ShellClasses = {
            "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow",
            "Windows.UI.Core.CoreWindow", "TopLevelWindowForOverflowXamlIsland", "XamlExplorerHostIslandWindow",
            "MultitaskingViewFrame", "ForegroundStaging", "Shell_InputSwitchTopLevelWindow", "#32768"
        };

        public static IntPtr Root(IntPtr h)
        {
            if (h == IntPtr.Zero) return h;
            var r = Native.GetAncestor(h, Native.GA_ROOT);
            return r == IntPtr.Zero ? h : r;
        }

        public static string ClassName(IntPtr h)
        {
            var sb = new StringBuilder(256);
            Native.GetClassName(h, sb, sb.Capacity);
            return sb.ToString();
        }

        public static bool IsManageable(IntPtr h)
        {
            if (h == IntPtr.Zero || !Native.IsWindow(h) || !Native.IsWindowVisible(h)) return false;
            int style = Native.GetWindowLong(h, Native.GWL_STYLE);
            if ((style & Native.WS_CHILD) != 0) return false;
            bool caption = (style & Native.WS_CAPTION) == Native.WS_CAPTION;
            bool sizable = (style & Native.WS_THICKFRAME) != 0;
            if (!caption && !sizable) return false;
            string cls = ClassName(h);
            foreach (var c in ShellClasses) if (cls == c) return false;
            int cloaked;
            if (Native.DwmGetWindowAttribute(h, Native.DWMWA_CLOAKED, out cloaked, 4) == 0 && cloaked != 0) return false;
            if (!AllowOwnProcess)
            {
                uint pid;
                Native.GetWindowThreadProcessId(h, out pid);
                if (pid == OwnPid) return false;
            }
            return true;
        }

        public static string ExePath(IntPtr h)
        {
            uint pid;
            Native.GetWindowThreadProcessId(h, out pid);
            if (pid == 0) return null;
            IntPtr ph = Native.OpenProcess(0x1000, false, pid);
            if (ph == IntPtr.Zero) return null;
            try
            {
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                return Native.QueryFullProcessImageName(ph, 0, sb, ref size) ? sb.ToString() : null;
            }
            finally { Native.CloseHandle(ph); }
        }

        // Nombre del ejecutable (p. ej. "Photoshop.exe"). Seguro para llamarlo desde cualquier hilo.
        public static string ExeName(IntPtr h)
        {
            lock (exeCache)
            {
                string s;
                if (exeCache.TryGetValue(h, out s)) return s;
                var p = ExePath(h);
                s = p == null ? null : System.IO.Path.GetFileName(p);
                if (exeCache.Count > 500) exeCache.Clear();
                exeCache[h] = s;
                return s;
            }
        }

        public static string AppDisplayName(IntPtr h)
        {
            try
            {
                var p = ExePath(h);
                if (p == null) return null;
                var fi = FileVersionInfo.GetVersionInfo(p);
                if (!string.IsNullOrEmpty(fi.FileDescription)) return fi.FileDescription.Trim();
                return System.IO.Path.GetFileNameWithoutExtension(p);
            }
            catch { return ExeName(h); }
        }

        public static int Dpi(IntPtr h)
        {
            try { int d = (int)Native.GetDpiForWindow(h); return d > 0 ? d : 96; } catch { return 96; }
        }

        // Coloca el marco VISIBLE de la ventana exactamente en 'target' (compensa los bordes invisibles de Windows 10/11)
        public static void SetFrame(IntPtr h, RECT target)
        {
            if (Native.IsIconic(h) || Native.IsZoomed(h)) Native.ShowWindow(h, Native.SW_RESTORE);
            IntPtr before = Native.MonitorFromWindow(h, Native.MONITOR_DEFAULTTONEAREST);
            Place(h, target);
            IntPtr after = Native.MonitorFromWindow(h, Native.MONITOR_DEFAULTTONEAREST);
            // Al cambiar de monitor (y de DPI) la ventana puede reajustarse: segunda pasada
            if (after != before || !Native.GetFrame(h).Near(target, 1)) Place(h, target);
        }

        static void Place(IntPtr h, RECT t)
        {
            RECT wr;
            Native.GetWindowRect(h, out wr);
            RECT fr = Native.GetFrame(h);
            int l = fr.Left - wr.Left, tp = fr.Top - wr.Top, r = wr.Right - fr.Right, b = wr.Bottom - fr.Bottom;
            if (l < 0 || l > 40) l = 0;
            if (tp < 0 || tp > 40) tp = 0;
            if (r < 0 || r > 40) r = 0;
            if (b < 0 || b > 40) b = 0;
            Native.SetWindowPos(h, IntPtr.Zero, t.Left - l, t.Top - tp, t.Width + l + r, t.Height + tp + b,
                Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_NOOWNERZORDER);
        }
    }

    public static class Layout
    {
        static readonly double[] Cycle = { 1.0 / 2, 2.0 / 3, 1.0 / 3 };

        public static RECT Frac(RECT w, double x0, double y0, double x1, double y1)
        {
            return new RECT(
                w.Left + (int)Math.Round(w.Width * x0), w.Top + (int)Math.Round(w.Height * y0),
                w.Left + (int)Math.Round(w.Width * x1), w.Top + (int)Math.Round(w.Height * y1));
        }

        // Fracción definida en horizontal; en pantallas verticales se intercambian los ejes (como Rectangle)
        static RECT Oriented(RECT w, bool portrait, double x0, double y0, double x1, double y1)
        {
            return portrait ? Frac(w, y0, x0, y1, x1) : Frac(w, x0, y0, x1, y1);
        }

        public static bool IsTiling(ActionId a)
        {
            switch (a)
            {
                case ActionId.Smaller: case ActionId.Larger: case ActionId.Center: case ActionId.Restore:
                case ActionId.NextDisplay: case ActionId.PreviousDisplay: case ActionId.AlmostMaximize:
                case ActionId.MoveLeft: case ActionId.MoveRight: case ActionId.MoveUp: case ActionId.MoveDown:
                case ActionId.MaximizeHeight: case ActionId.None:
                    return false;
            }
            return true;
        }

        // Acción efectiva tras repeticiones (tercios que rotan, etc.)
        public static ActionId Effective(ActionId a, int count, int repeatMode)
        {
            if (count <= 0 || repeatMode == 2) return a;
            switch (a)
            {
                case ActionId.FirstThird: return new[] { ActionId.FirstThird, ActionId.CenterThird, ActionId.LastThird }[count % 3];
                case ActionId.LastThird: return new[] { ActionId.LastThird, ActionId.CenterThird, ActionId.FirstThird }[count % 3];
                case ActionId.FirstTwoThirds: return count % 2 == 0 ? ActionId.FirstTwoThirds : ActionId.LastTwoThirds;
                case ActionId.LastTwoThirds: return count % 2 == 0 ? ActionId.LastTwoThirds : ActionId.FirstTwoThirds;
                case ActionId.FirstFourth: return new[] { ActionId.FirstFourth, ActionId.SecondFourth, ActionId.ThirdFourth, ActionId.LastFourth }[count % 4];
                case ActionId.LastFourth: return new[] { ActionId.LastFourth, ActionId.ThirdFourth, ActionId.SecondFourth, ActionId.FirstFourth }[count % 4];
            }
            return a;
        }

        public static bool CyclesSize(ActionId a)
        {
            switch (a)
            {
                case ActionId.LeftHalf: case ActionId.RightHalf: case ActionId.CenterHalf:
                case ActionId.TopHalf: case ActionId.BottomHalf:
                case ActionId.TopLeft: case ActionId.TopRight: case ActionId.BottomLeft: case ActionId.BottomRight:
                    return true;
            }
            return false;
        }

        // Rectángulo de una acción de mosaico dentro del área de trabajo w (sin huecos)
        public static RECT Tile(ActionId a, RECT w, bool portrait, int sizeCycle)
        {
            double f = Cycle[((sizeCycle % 3) + 3) % 3];
            const double t1 = 1.0 / 3, t2 = 2.0 / 3;
            switch (a)
            {
                case ActionId.LeftHalf: return Frac(w, 0, 0, f, 1);
                case ActionId.RightHalf: return Frac(w, 1 - f, 0, 1, 1);
                case ActionId.CenterHalf: return Frac(w, (1 - f) / 2, 0, (1 + f) / 2, 1);
                case ActionId.TopHalf: return Frac(w, 0, 0, 1, f);
                case ActionId.BottomHalf: return Frac(w, 0, 1 - f, 1, 1);
                case ActionId.TopLeft: return Frac(w, 0, 0, f, 0.5);
                case ActionId.TopRight: return Frac(w, 1 - f, 0, 1, 0.5);
                case ActionId.BottomLeft: return Frac(w, 0, 0.5, f, 1);
                case ActionId.BottomRight: return Frac(w, 1 - f, 0.5, 1, 1);
                case ActionId.FirstThird: return Oriented(w, portrait, 0, 0, t1, 1);
                case ActionId.CenterThird: return Oriented(w, portrait, t1, 0, t2, 1);
                case ActionId.LastThird: return Oriented(w, portrait, t2, 0, 1, 1);
                case ActionId.FirstTwoThirds: return Oriented(w, portrait, 0, 0, t2, 1);
                case ActionId.CenterTwoThirds: return Oriented(w, portrait, 1.0 / 6, 0, 5.0 / 6, 1);
                case ActionId.LastTwoThirds: return Oriented(w, portrait, t1, 0, 1, 1);
                case ActionId.Maximize: return w;
                case ActionId.FirstFourth: return Oriented(w, portrait, 0, 0, 0.25, 1);
                case ActionId.SecondFourth: return Oriented(w, portrait, 0.25, 0, 0.5, 1);
                case ActionId.ThirdFourth: return Oriented(w, portrait, 0.5, 0, 0.75, 1);
                case ActionId.LastFourth: return Oriented(w, portrait, 0.75, 0, 1, 1);
                case ActionId.FirstThreeFourths: return Oriented(w, portrait, 0, 0, 0.75, 1);
                case ActionId.LastThreeFourths: return Oriented(w, portrait, 0.25, 0, 1, 1);
                case ActionId.TopLeftSixth: return Oriented(w, portrait, 0, 0, t1, 0.5);
                case ActionId.TopCenterSixth: return Oriented(w, portrait, t1, 0, t2, 0.5);
                case ActionId.TopRightSixth: return Oriented(w, portrait, t2, 0, 1, 0.5);
                case ActionId.BottomLeftSixth: return Oriented(w, portrait, 0, 0.5, t1, 1);
                case ActionId.BottomCenterSixth: return Oriented(w, portrait, t1, 0.5, t2, 1);
                case ActionId.BottomRightSixth: return Oriented(w, portrait, t2, 0.5, 1, 1);
            }
            return w;
        }

        public static RECT ApplyGaps(RECT r, RECT w, int gap)
        {
            if (gap <= 0) return r;
            int h1 = gap / 2, h2 = gap - h1;
            return new RECT(
                r.Left + (r.Left <= w.Left ? gap : h2),
                r.Top + (r.Top <= w.Top ? gap : h2),
                r.Right - (r.Right >= w.Right ? gap : h1),
                r.Bottom - (r.Bottom >= w.Bottom ? gap : h1));
        }

        public static RECT AlmostMaximize(RECT w)
        {
            return Frac(w, 0.05, 0.05, 0.95, 0.95);
        }

        public static RECT Clamp(RECT r, RECT w)
        {
            int width = Math.Min(r.Width, w.Width), height = Math.Min(r.Height, w.Height);
            int x = Math.Max(w.Left, Math.Min(r.Left, w.Right - width));
            int y = Math.Max(w.Top, Math.Min(r.Top, w.Bottom - height));
            return RECT.XYWH(x, y, width, height);
        }

        public static RECT CenterIn(RECT cur, RECT w)
        {
            int width = Math.Min(cur.Width, w.Width), height = Math.Min(cur.Height, w.Height);
            return RECT.XYWH(w.Left + (w.Width - width) / 2, w.Top + (w.Height - height) / 2, width, height);
        }

        // Más grande / más pequeña: 30 px en total (como Rectangle); los lados pegados al borde se quedan pegados
        public static RECT Resize(RECT cur, RECT w, int step, bool larger, int gap)
        {
            int tol = gap + 2;
            bool atL = cur.Left <= w.Left + tol, atR = cur.Right >= w.Right - tol;
            bool atT = cur.Top <= w.Top + tol, atB = cur.Bottom >= w.Bottom - tol;
            int d = larger ? step : -step;
            int minW = Math.Min(cur.Width, Math.Max(w.Width / 4, 200)), minH = Math.Min(cur.Height, Math.Max(w.Height / 4, 150));
            int nw = Math.Max(minW, Math.Min(w.Width, cur.Width + d));
            int nh = Math.Max(minH, Math.Min(w.Height, cur.Height + d));
            int x, y;
            if (atL && !atR) x = cur.Left;
            else if (atR && !atL) x = cur.Right - nw;
            else x = cur.Left - (nw - cur.Width) / 2;
            if (atT && !atB) y = cur.Top;
            else if (atB && !atT) y = cur.Bottom - nh;
            else y = cur.Top - (nh - cur.Height) / 2;
            return Clamp(RECT.XYWH(x, y, nw, nh), w);
        }
    }

    public sealed class WindowState
    {
        public ActionId Last = ActionId.None;
        public int Count;
        public RECT LastRect;
        public RECT RestoreRect;
        public bool WasMaximized;
    }

    public sealed class Executor
    {
        readonly Func<Config> config;
        readonly Dictionary<IntPtr, WindowState> states = new Dictionary<IntPtr, WindowState>();

        public Executor(Func<Config> cfg) { config = cfg; }

        public void Forget(IntPtr h) { states.Remove(h); }

        public WindowState StateOf(IntPtr h)
        {
            WindowState s;
            return states.TryGetValue(h, out s) ? s : null;
        }

        void Prune()
        {
            if (states.Count < 200) return;
            var dead = new List<IntPtr>();
            foreach (var k in states.Keys) if (!Native.IsWindow(k)) dead.Add(k);
            foreach (var k in dead) states.Remove(k);
        }

        static int Scaled(IntPtr h, int v) { return (int)Math.Round(v * Win.Dpi(h) / 96.0); }

        // Ejecuta una acción sobre la ventana. 'restoreOverride' se usa al soltar una ventana arrastrada.
        public bool Run(ActionId a, IntPtr h, bool fromDrag, RECT restoreOverride)
        {
            if (a == ActionId.None || h == IntPtr.Zero || !Native.IsWindow(h) || Native.IsHungAppWindow(h)) return false;
            Prune();
            var cfg = config();
            int gap = Math.Max(0, cfg.Gap);
            bool zoomed = Native.IsZoomed(h);
            RECT cur = Native.GetFrame(h);
            WindowState st = StateOf(h);
            bool managed = st != null && !zoomed && !fromDrag && st.LastRect.Near(cur, 2);

            if (a == ActionId.Restore)
            {
                if (st == null) return false;
                Win.SetFrame(h, st.RestoreRect);
                if (st.WasMaximized) Native.ShowWindow(h, Native.SW_MAXIMIZE);
                states.Remove(h);
                return true;
            }

            if (zoomed)
            {
                Native.ShowWindow(h, Native.SW_RESTORE);
                cur = Native.GetFrame(h);
            }

            if (st == null || (!managed && !fromDrag))
            {
                st = new WindowState();
                st.RestoreRect = cur;
                st.WasMaximized = zoomed;
                states[h] = st;
            }
            else if (fromDrag && !st.LastRect.Near(restoreOverride, 2))
            {
                // la ventana arrastrada no venía de un ajuste nuestro: su tamaño previo es el de restaurar
                st.RestoreRect = restoreOverride;
                st.WasMaximized = false;
            }
            if (fromDrag && st.Last == ActionId.None) st.RestoreRect = restoreOverride;

            int count = (managed && st.Last == a) ? st.Count + 1 : 0;
            var mon = Monitors.FromWindow(h);
            if (mon == null) return false;
            RECT w = mon.Work;
            ActionId record = a;
            RECT target;

            switch (a)
            {
                case ActionId.NextDisplay:
                case ActionId.PreviousDisplay:
                {
                    var all = Monitors.All();
                    if (all.Count < 2) return false;
                    int idx = 0;
                    for (int i = 0; i < all.Count; i++) if (all[i].Handle == mon.Handle) idx = i;
                    var dst = all[(idx + (a == ActionId.NextDisplay ? 1 : all.Count - 1)) % all.Count];
                    bool reapply = managed && st.Last != ActionId.None && Layout.IsTiling(st.Last);
                    if (reapply)
                    {
                        target = TileFor(st.Last, st.Count, dst, cfg, gap);
                        record = st.Last;
                        count = st.Count;
                    }
                    else
                    {
                        double rx = (cur.Left - w.Left) / (double)Math.Max(1, w.Width - cur.Width);
                        double ry = (cur.Top - w.Top) / (double)Math.Max(1, w.Height - cur.Height);
                        int nw = Math.Min(cur.Width, dst.Work.Width), nh = Math.Min(cur.Height, dst.Work.Height);
                        int x = dst.Work.Left + (int)Math.Round(Math.Max(0, Math.Min(1, rx)) * (dst.Work.Width - nw));
                        int y = dst.Work.Top + (int)Math.Round(Math.Max(0, Math.Min(1, ry)) * (dst.Work.Height - nh));
                        target = RECT.XYWH(x, y, nw, nh);
                        record = ActionId.None;
                    }
                    Win.SetFrame(h, target);
                    if (zoomed)
                    {
                        Native.ShowWindow(h, Native.SW_MAXIMIZE);
                        states.Remove(h);
                        return true;
                    }
                    break;
                }
                case ActionId.AlmostMaximize: target = Layout.AlmostMaximize(w); Win.SetFrame(h, target); break;
                case ActionId.MaximizeHeight:
                    target = Layout.Clamp(new RECT(cur.Left, w.Top + gap, cur.Right, w.Bottom - gap), w);
                    Win.SetFrame(h, target); break;
                case ActionId.Center: target = Layout.CenterIn(cur, w); Win.SetFrame(h, target); break;
                case ActionId.Smaller:
                case ActionId.Larger:
                    target = Layout.Resize(cur, w, Scaled(h, 30), a == ActionId.Larger, gap);
                    Win.SetFrame(h, target); break;
                case ActionId.MoveLeft:
                case ActionId.MoveRight:
                case ActionId.MoveUp:
                case ActionId.MoveDown:
                {
                    RECT c = Layout.Clamp(cur, new RECT(w.Left + gap, w.Top + gap, w.Right - gap, w.Bottom - gap));
                    int x = c.Left, y = c.Top;
                    if (a == ActionId.MoveLeft) x = w.Left + gap;
                    if (a == ActionId.MoveRight) x = w.Right - gap - c.Width;
                    if (a == ActionId.MoveUp) y = w.Top + gap;
                    if (a == ActionId.MoveDown) y = w.Bottom - gap - c.Height;
                    target = RECT.XYWH(x, y, c.Width, c.Height);
                    Win.SetFrame(h, target); break;
                }
                default:
                {
                    // Comando repetido + "mover a pantalla contigua": izquierda/derecha saltan de monitor
                    if (count > 0 && cfg.RepeatMode == 1 && (a == ActionId.LeftHalf || a == ActionId.RightHalf))
                    {
                        var adj = Adjacent(mon, a == ActionId.LeftHalf);
                        if (adj != null)
                        {
                            record = a == ActionId.LeftHalf ? ActionId.RightHalf : ActionId.LeftHalf;
                            target = TileFor(record, 0, adj, cfg, gap);
                            count = 0;
                            Win.SetFrame(h, target);
                            break;
                        }
                        count = 0;
                    }
                    target = TileFor(a, count, mon, cfg, gap);
                    Win.SetFrame(h, target);
                    break;
                }
            }

            st.Last = record;
            st.Count = count;
            st.LastRect = Native.GetFrame(h);
            return true;
        }

        public static RECT TileFor(ActionId a, int count, Monitor mon, Config cfg, int gap)
        {
            var eff = Layout.Effective(a, count, cfg.RepeatMode);
            int sizeCycle = (cfg.RepeatMode == 0 && Layout.CyclesSize(a)) ? count : 0;
            if (eff == ActionId.AlmostMaximize) return Layout.AlmostMaximize(mon.Work);
            return Layout.ApplyGaps(Layout.Tile(eff, mon.Work, mon.Portrait, sizeCycle), mon.Work, gap);
        }

        static Monitor Adjacent(Monitor m, bool left)
        {
            Monitor best = null;
            int bestDist = int.MaxValue;
            foreach (var o in Monitors.All())
            {
                if (o.Handle == m.Handle) continue;
                int cx = (o.Bounds.Left + o.Bounds.Right) / 2, mx = (m.Bounds.Left + m.Bounds.Right) / 2;
                if (left ? cx >= mx : cx <= mx) continue;
                int dist = Math.Abs(cx - mx) + Math.Abs((o.Bounds.Top + o.Bounds.Bottom) / 2 - (m.Bounds.Top + m.Bounds.Bottom) / 2);
                if (dist < bestDist) { bestDist = dist; best = o; }
            }
            return best;
        }

        // Al soltar fuera de un área una ventana que estaba ajustada: recupera su tamaño anterior
        public bool UnsnapRestore(IntPtr h, RECT dragStart, POINT cursor)
        {
            var st = StateOf(h);
            if (st == null || !st.LastRect.Near(dragStart, 2)) return false;
            RECT cur = Native.GetFrame(h);
            int nw = st.RestoreRect.Width, nh = st.RestoreRect.Height;
            if (nw <= 0 || nh <= 0) return false;
            double rel = cur.Width > 0 ? (cursor.X - cur.Left) / (double)cur.Width : 0.5;
            int x = cursor.X - (int)Math.Round(rel * nw);
            var mon = Monitors.FromPoint(cursor.X, cursor.Y);
            RECT t = RECT.XYWH(x, cur.Top, nw, nh);
            if (mon != null) t = Layout.Clamp(t, mon.Work);
            Win.SetFrame(h, t);
            states.Remove(h);
            return true;
        }
    }
}
