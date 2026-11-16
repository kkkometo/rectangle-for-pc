// Genera los fotogramas de docs/demo.gif. No es una grabación de pantalla: cada fotograma se dibuja
// aquí, con la geometría real de la app (Layout.Tile, ApplyGaps), sus iconos y su vista previa.
// Uso: demo.exe <carpeta de salida>   → frame_00000.png, frame_00001.png, ... a 25 fps
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

namespace Encuadre.Demo
{
    static class Ease
    {
        public static double Clamp(double v) { return v < 0 ? 0 : v > 1 ? 1 : v; }
        public static double OutCubic(double p) { p = Clamp(p); return 1 - Math.Pow(1 - p, 3); }
        public static double InOutCubic(double p) { p = Clamp(p); return p < 0.5 ? 4 * p * p * p : 1 - Math.Pow(-2 * p + 2, 3) / 2; }
        public static float Lerp(float a, float b, double p) { return (float)(a + (b - a) * p); }
        public static RectangleF Lerp(RectangleF a, RectangleF b, double p)
        {
            return new RectangleF(Lerp(a.X, b.X, p), Lerp(a.Y, b.Y, p), Lerp(a.Width, b.Width, p), Lerp(a.Height, b.Height, p));
        }
        // 0 → 1 en [t0, t0 + fadeIn], 1 → 0 en [t1 - fadeOut, t1]
        public static double Window(double t, double t0, double t1, double fadeIn, double fadeOut)
        {
            if (t < t0 || t > t1) return 0;
            double a = fadeIn > 0 ? Clamp((t - t0) / fadeIn) : 1;
            double b = fadeOut > 0 ? Clamp((t1 - t) / fadeOut) : 1;
            return Math.Min(a, b);
        }
        public static int A(double a, int max) { return Math.Max(0, Math.Min(255, (int)Math.Round(a * max))); }
    }

    struct RKey { public double T; public RectangleF R; public double Dur; }

    sealed class AppWin
    {
        public string Title;
        public int Kind;
        public Color Accent;
        public RectangleF Initial;
        public readonly List<RKey> Keys = new List<RKey>();
        public readonly List<double> Raises = new List<double>();

        public void Move(double t, RectangleF r, double dur) { Keys.Add(new RKey { T = t, R = r, Dur = dur }); }

        public RectangleF At(double t)
        {
            RectangleF cur = Initial;
            foreach (var k in Keys)
            {
                if (k.T > t) break;
                if (k.Dur > 0 && t < k.T + k.Dur) return Ease.Lerp(cur, k.R, Ease.OutCubic((t - k.T) / k.Dur));
                cur = k.R;
            }
            return cur;
        }

        public double LastRaise(double t)
        {
            double best = double.MinValue;
            foreach (var r in Raises) if (r <= t && r > best) best = r;
            return best;
        }
    }

    // Atajo en pantalla. Keys[0..n-2] son modificadores que bajan en Down[i] y se sueltan en Release;
    // la última tecla se toca en cada Taps[j] (ahí ocurre la acción, con Labels[j] / Icons[j]).
    sealed class Shot
    {
        public double T0, T1, Release;
        public string[] Keys;
        public double[] Down, Taps;
        public string[] Labels;
        public ActionId[] Icons;
    }
    sealed class Flash { public double T; public AppWin W; }
    sealed class Caption { public double T0, T1; public string Title, Sub; }
    sealed class Foot { public double T0, T1; public RectangleF R; }
    sealed class Badge { public double T; public AppWin W; public string Text; }
    sealed class CKey { public double T; public float X, Y; }

    // Guion: qué pasa y cuándo (segundos). Coordenadas relativas al escritorio (840 × 442 útiles).
    sealed class Story
    {
        public const int Gap = 6;
        public static readonly RECT Work = new RECT(0, 0, 840, 442);
        public const double Duration = 46.0;

        public readonly List<AppWin> Wins = new List<AppWin>();
        public readonly List<Shot> Shots = new List<Shot>();
        public readonly List<Flash> Flashes = new List<Flash>();
        public readonly List<Caption> Captions = new List<Caption>();
        public readonly List<Foot> Feet = new List<Foot>();
        public readonly List<Badge> Badges = new List<Badge>();
        public readonly List<CKey> Cursor = new List<CKey>();
        public readonly List<double[]> CursorVisible = new List<double[]>();
        public readonly List<double> Clicks = new List<double>();

        AppWin browser, notes, terminal, photos;

        static RectangleF F(RECT r) { return new RectangleF(r.Left, r.Top, r.Width, r.Height); }
        static RectangleF TileRect(ActionId a, int cycle) { return F(Layout.ApplyGaps(Layout.Tile(a, Work, false, cycle), Work, Gap)); }

        AppWin NewWin(string title, int kind, Color accent, RectangleF r, double z)
        {
            var w = new AppWin { Title = title, Kind = kind, Accent = accent, Initial = r };
            w.Raises.Add(z);
            Wins.Add(w);
            return w;
        }

        void Snap(AppWin w, double t, ActionId a, int cycle) { MoveTo(w, t, TileRect(a, cycle)); }
        void MoveTo(AppWin w, double t, RectangleF r)
        {
            w.Move(t, r, 0.16);
            w.Raises.Add(t);
            Flashes.Add(new Flash { T = t, W = w });
        }

        // Atajo completo con acción en t: aparecen las teclas, baja Win, baja Alt, se toca la tecla, se sueltan.
        void Combo(double t, double end, string key, string label, ActionId icon)
        {
            Shots.Add(new Shot
            {
                T0 = t - 0.8, T1 = end, Release = t + 0.45,
                Keys = new[] { "Win", "Alt", key }, Down = new[] { t - 0.55, t - 0.3 },
                Taps = new[] { t }, Labels = new[] { label }, Icons = new[] { icon }
            });
        }

        // Solo la etiqueta (al arrastrar no hay teclas)
        void Hint(double t0, double t1, string label, ActionId icon)
        {
            Shots.Add(new Shot
            {
                T0 = t0, T1 = t1, Release = t1, Keys = new string[0], Down = new double[0],
                Taps = new[] { t0 }, Labels = new[] { label }, Icons = new[] { icon }
            });
        }
        void Cap(double t0, double t1, string title, string sub) { Captions.Add(new Caption { T0 = t0, T1 = t1, Title = title, Sub = sub }); }
        void Cur(double t, float x, float y) { Cursor.Add(new CKey { T = t, X = x, Y = y }); }

        public PointF CursorAt(double t)
        {
            if (Cursor.Count == 0) return PointF.Empty;
            if (t <= Cursor[0].T) return new PointF(Cursor[0].X, Cursor[0].Y);
            for (int i = 0; i < Cursor.Count - 1; i++)
            {
                var a = Cursor[i]; var b = Cursor[i + 1];
                if (t >= a.T && t < b.T)
                {
                    double p = Ease.InOutCubic((t - a.T) / (b.T - a.T));
                    return new PointF(Ease.Lerp(a.X, b.X, p), Ease.Lerp(a.Y, b.Y, p));
                }
            }
            var l = Cursor[Cursor.Count - 1];
            return new PointF(l.X, l.Y);
        }

        void Drag(AppWin w, double t0, double t1, PointF grab)
        {
            var size = w.At(t0).Size;
            for (double t = t0; t <= t1 + 1e-9; t += 1.0 / 50)
            {
                var p = CursorAt(t);
                w.Move(t, new RectangleF(p.X - grab.X, p.Y - grab.Y, size.Width, size.Height), 0);
            }
        }

        public Story()
        {
            browser = NewWin("Browser", 0, Color.FromArgb(77, 141, 255), new RectangleF(40, 36, 420, 290), -1);
            notes = NewWin("Notes", 1, Color.FromArgb(255, 184, 77), new RectangleF(330, 70, 350, 250), -2);
            terminal = NewWin("Terminal", 2, Color.FromArgb(80, 205, 125), new RectangleF(110, 210, 380, 196), -3);
            photos = NewWin("Photos", 3, Color.FromArgb(236, 100, 160), new RectangleF(470, 170, 330, 230), -4);

            // 0–2.6 portada · 2.6–6.8 explicación "Hold Win + Alt" · 6.8–42.4 escritorio · 42.6– cierre

            // ---- textos ----
            Cap(6.9, 12.3, "Snap to halves", "Hold Win + Alt, then tap Left or Right");
            Cap(12.5, 17.5, "Tap again to resize", "Keep holding Win + Alt to go from ½ to ⅔ to ⅓");
            Cap(17.7, 24.3, "Corners", "Win + Alt + U, I, J or K");
            Cap(24.5, 29.9, "Thirds", "Win + Alt + D, F or G");
            Cap(30.1, 35.7, "Maximize, restore, center", "Win + Alt + Enter, Backspace or C");
            Cap(35.9, 42.4, "Or drag to an edge", "A preview shows where the window will land");

            // ---- cursor ----
            CursorVisible.Add(new[] { 6.9, 17.6 });
            CursorVisible.Add(new[] { 35.8, 42.4 });
            Cur(6.9, 430, 300); Cur(7.4, 210, 49); Cur(8.9, 210, 49); Cur(9.4, 560, 83); Cur(11.9, 560, 83); Cur(12.3, 160, 19);
            const double d = 10.4; // el arrastre empieza 10.4 s más tarde que en la primera versión
            Cur(25.4 + d, 600, 380); Cur(26.3 + d, 420, 136); Cur(26.5 + d, 420, 136); Cur(27.6 + d, 3, 222); Cur(28.3 + d, 3, 222);
            Cur(28.95 + d, 490, 19); Cur(29.1 + d, 490, 19); Cur(29.75 + d, 430, 2); Cur(30.75 + d, 836, 2);
            Clicks.AddRange(new[] { 7.5, 9.5, 12.4, 26.45 + d, 29.05 + d });

            // ---- mitades ----
            browser.Raises.Add(7.5);
            Snap(browser, 8.4, ActionId.LeftHalf, 0);
            Combo(8.4, 9.3, "←", "Left Half", ActionId.LeftHalf);
            notes.Raises.Add(9.5);
            Snap(notes, 10.6, ActionId.RightHalf, 0);
            Combo(10.6, 11.5, "→", "Right Half", ActionId.RightHalf);

            // ---- ciclo ½ → ⅔ → ⅓ → ½, con Win + Alt sostenidas ----
            browser.Raises.Add(12.4);
            string[] fr = { "½", "⅔", "⅓" };
            double[] taps = { 13.55, 14.65, 15.75 };
            int[] cyc = { 1, 2, 0 };
            var labels = new string[3];
            for (int i = 0; i < 3; i++)
            {
                Snap(browser, taps[i], ActionId.LeftHalf, cyc[i]);
                Badges.Add(new Badge { T = taps[i], W = browser, Text = fr[cyc[i]] });
                labels[i] = "Left Half · " + fr[cyc[i]];
            }
            Shots.Add(new Shot
            {
                T0 = 12.75, T1 = 16.8, Release = 16.25, Keys = new[] { "Win", "Alt", "←" }, Down = new[] { 12.95, 13.15 },
                Taps = taps, Labels = labels, Icons = new[] { ActionId.LeftHalf, ActionId.LeftHalf, ActionId.LeftHalf }
            });

            // ---- esquinas (cada 1.6 s) ----
            Snap(browser, 18.5, ActionId.TopLeft, 0); Combo(18.5, 19.3, "U", "Top Left", ActionId.TopLeft);
            Snap(notes, 20.1, ActionId.TopRight, 0); Combo(20.1, 20.9, "I", "Top Right", ActionId.TopRight);
            Snap(terminal, 21.7, ActionId.BottomLeft, 0); Combo(21.7, 22.5, "J", "Bottom Left", ActionId.BottomLeft);
            Snap(photos, 23.3, ActionId.BottomRight, 0); Combo(23.3, 24.25, "K", "Bottom Right", ActionId.BottomRight);

            // ---- tercios ----
            Snap(browser, 25.3, ActionId.FirstThird, 0); Combo(25.3, 26.1, "D", "First Third", ActionId.FirstThird);
            Snap(notes, 26.9, ActionId.CenterThird, 0); Combo(26.9, 27.7, "F", "Center Third", ActionId.CenterThird);
            Snap(terminal, 28.5, ActionId.LastThird, 0); Combo(28.5, 29.45, "G", "Last Third", ActionId.LastThird);

            // ---- maximizar, restaurar, centrar ----
            Snap(terminal, 30.9, ActionId.Maximize, 0); Combo(30.9, 31.7, "Enter", "Maximize", ActionId.Maximize);
            MoveTo(terminal, 32.5, terminal.Initial); Combo(32.5, 33.3, "Backspace", "Restore", ActionId.Restore);
            var centered = new RectangleF((840 - 380) / 2, (442 - 196) / 2, 380, 196);
            MoveTo(terminal, 34.1, centered); Combo(34.1, 35.05, "C", "Center", ActionId.Center);

            // ---- arrastrar: borde izquierdo, luego arriba y a la esquina ----
            terminal.Raises.Add(26.45 + d);
            Drag(terminal, 26.5 + d, 28.3 + d, new PointF(190, 13));
            Snap(terminal, 28.3 + d, ActionId.LeftHalf, 0);
            Feet.Add(new Foot { T0 = 27.56 + d, T1 = 28.3 + d, R = TileRect(ActionId.LeftHalf, 0) });
            Hint(27.56 + d, 28.8 + d, "Left Half", ActionId.LeftHalf);

            notes.Raises.Add(29.05 + d);
            Drag(notes, 29.1 + d, 31.15 + d, new PointF(207, 13));
            Snap(notes, 31.15 + d, ActionId.TopRight, 0);
            Feet.Add(new Foot { T0 = 29.72 + d, T1 = 30.65 + d, R = TileRect(ActionId.Maximize, 0) });
            Feet.Add(new Foot { T0 = 30.65 + d, T1 = 31.15 + d, R = TileRect(ActionId.TopRight, 0) });
            Hint(29.72 + d, 30.65 + d, "Maximize", ActionId.Maximize);
            Hint(30.65 + d, 31.8 + d, "Top Right", ActionId.TopRight);

            foreach (var w in Wins) w.Keys.Sort(delegate(RKey a, RKey b) { return a.T.CompareTo(b.T); });
        }
    }

    sealed class Renderer
    {
        public const int W = 960, H = 690;
        static readonly Rectangle Scr = new Rectangle(60, 84, 840, 472);
        const float HudTop = 596, KeyH = 56;
        const int TaskbarH = 30;
        static readonly int WorkH = Scr.Height - TaskbarH;

        readonly Story s;
        readonly Bitmap frame = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        Bitmap stageBase, taskbar, titleCard, endCard;

        static readonly string Sans = "Segoe UI", Semi = "Segoe UI Semibold";
        readonly Font fCapTitle = new Font(Semi, 27, GraphicsUnit.Pixel);
        readonly Font fCapSub = new Font(Sans, 15.5f, GraphicsUnit.Pixel);
        readonly Font fKey = new Font(Semi, 22, GraphicsUnit.Pixel);
        readonly Font fPlus = new Font(Sans, 24, GraphicsUnit.Pixel);
        readonly Font fLabel = new Font(Semi, 22, GraphicsUnit.Pixel);
        readonly Font fFrac = new Font(Semi, 28, GraphicsUnit.Pixel);
        readonly Font fBigKey = new Font(Semi, 34, GraphicsUnit.Pixel);
        readonly Font fBigPlus = new Font(Sans, 36, GraphicsUnit.Pixel);
        readonly Font fExplain = new Font(Semi, 40, GraphicsUnit.Pixel);
        readonly Font fExplainSub = new Font(Sans, 19, GraphicsUnit.Pixel);
        Bitmap backdrop;
        readonly Font fWinTitle = new Font(Sans, 11, GraphicsUnit.Pixel);
        readonly Font fBadge = new Font(Semi, 40, GraphicsUnit.Pixel);
        readonly Font fClock = new Font(Sans, 11, GraphicsUnit.Pixel);
        readonly StringFormat center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
        readonly StringFormat tight = StringFormat.GenericTypographic;

        public Renderer(Story story)
        {
            s = story;
            BuildCaches();
        }

        static void Hq(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.CompositingQuality = CompositingQuality.HighQuality;
        }

        static void Glow(Graphics g, float cx, float cy, float rx, float ry, Color c)
        {
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(cx - rx, cy - ry, 2 * rx, 2 * ry);
                using (var b = new PathGradientBrush(path) { CenterColor = c, SurroundColors = new[] { Color.FromArgb(0, c) } })
                    g.FillPath(b, path);
            }
        }

        void Backdrop(Graphics g)
        {
            g.Clear(Color.FromArgb(9, 10, 15));
            Glow(g, W * 0.25f, H * 0.15f, 620, 420, Color.FromArgb(70, 60, 80, 190));
            Glow(g, W * 0.85f, H * 0.95f, 560, 380, Color.FromArgb(60, 130, 60, 180));
        }

        void BuildCaches()
        {
            // fondo + monitor + fondo de escritorio
            stageBase = new Bitmap(W, H, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(stageBase))
            {
                Hq(g);
                Backdrop(g);
                for (int i = 14; i >= 1; i--)
                    using (var p = Theme.Rounded(new RectangleF(Scr.X - 8 - i, Scr.Y - 8 - i + 10, Scr.Width + 16 + 2 * i, Scr.Height + 16 + 2 * i), 18 + i))
                    using (var b = new SolidBrush(Color.FromArgb(9, 0, 0, 0))) g.FillPath(b, p);
                using (var p = Theme.Rounded(new RectangleF(Scr.X - 8, Scr.Y - 8, Scr.Width + 16, Scr.Height + 16), 18))
                {
                    using (var b = new SolidBrush(Color.FromArgb(6, 6, 9))) g.FillPath(b, p);
                    using (var pen = new Pen(Color.FromArgb(34, 255, 255, 255), 1)) g.DrawPath(pen, p);
                }
                using (var p = Theme.Rounded(new RectangleF(Scr.X, Scr.Y, Scr.Width, Scr.Height), 10))
                {
                    var st = g.Save();
                    g.SetClip(p);
                    using (var b = new LinearGradientBrush(Scr, Color.FromArgb(26, 38, 86), Color.FromArgb(74, 40, 108), 32f)) g.FillRectangle(b, Scr);
                    Glow(g, Scr.X + 160, Scr.Y + 90, 380, 260, Color.FromArgb(70, 110, 140, 255));
                    Glow(g, Scr.Right - 140, Scr.Bottom - 80, 360, 240, Color.FromArgb(60, 240, 110, 200));
                    // curvas suaves al estilo del fondo de Windows 11
                    using (var pen = new Pen(Color.FromArgb(26, 255, 255, 255), 60f))
                    {
                        g.DrawBezier(pen, Scr.X - 60, Scr.Bottom - 40, Scr.X + 260, Scr.Y + 140, Scr.X + 520, Scr.Bottom + 60, Scr.Right + 80, Scr.Y + 120);
                    }
                    using (var pen = new Pen(Color.FromArgb(18, 255, 255, 255), 26f))
                    {
                        g.DrawBezier(pen, Scr.X - 40, Scr.Y + 70, Scr.X + 300, Scr.Y - 20, Scr.X + 560, Scr.Y + 230, Scr.Right + 60, Scr.Y + 40);
                    }
                    g.Restore(st);
                }
            }

            // barra de tareas (se dibuja encima de las ventanas)
            taskbar = new Bitmap(Scr.Width, TaskbarH, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(taskbar))
            {
                Hq(g);
                using (var b = new SolidBrush(Color.FromArgb(240, 22, 22, 28))) g.FillRectangle(b, 0, 0, Scr.Width, TaskbarH);
                using (var pen = new Pen(Color.FromArgb(40, 255, 255, 255))) g.DrawLine(pen, 0, 0, Scr.Width, 0);
                for (int i = 0; i < s.Wins.Count; i++) TaskIcon(g, i, s.Wins[i]);
                using (var tray = Icons.TrayBitmap(16, false, 0)) g.DrawImage(tray, Scr.Width - 74, 7, 16, 16);
                using (var b = new SolidBrush(Color.FromArgb(225, 230, 230, 236))) g.DrawString("9:41", fClock, b, Scr.Width - 46, 8);
            }

            titleCard = Card(true);
            endCard = Card(false);
            backdrop = new Bitmap(W, H, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(backdrop)) { Hq(g); Backdrop(g); }
        }

        float TaskX(int i) { return Scr.Width / 2f - s.Wins.Count * 34 / 2f + i * 34 + 8; }

        void TaskIcon(Graphics g, int i, AppWin w)
        {
            var r = new RectangleF(TaskX(i), 6, 18, 18);
            using (var p = Theme.Rounded(r, 5))
            using (var b = new LinearGradientBrush(r, System.Windows.Forms.ControlPaint.Light(w.Accent, 0.25f), w.Accent, 90f)) g.FillPath(b, p);
            using (var pen = new Pen(Color.FromArgb(235, 255, 255, 255), 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                float cx = r.X + 9, cy = r.Y + 9;
                switch (w.Kind)
                {
                    case 0: g.DrawEllipse(pen, cx - 4.5f, cy - 4.5f, 9, 9); g.DrawLine(pen, cx - 4.5f, cy, cx + 4.5f, cy); break;
                    case 1: for (int k = -1; k <= 1; k++) g.DrawLine(pen, cx - 4, cy + k * 3.2f, cx + (k == 1 ? 1 : 4), cy + k * 3.2f); break;
                    case 2: g.DrawLines(pen, new[] { new PointF(cx - 4, cy - 3.5f), new PointF(cx, cy), new PointF(cx - 4, cy + 3.5f) }); g.DrawLine(pen, cx + 1, cy + 4, cx + 4.5f, cy + 4); break;
                    default: g.DrawLines(pen, new[] { new PointF(cx - 5, cy + 4), new PointF(cx - 1.5f, cy - 1), new PointF(cx + 1, cy + 2), new PointF(cx + 2.5f, cy), new PointF(cx + 5, cy + 4) }); break;
                }
            }
        }

        Bitmap Card(bool title)
        {
            var b = new Bitmap(W, H, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(b))
            {
                Hq(g);
                Backdrop(g);
                int logo = title ? 104 : 84;
                float y = title ? 172 : 180;
                using (var l = Icons.Logo(logo)) g.DrawImage(l, (W - logo) / 2f, y, logo, logo);
                y += logo + 22;
                using (var f = new Font(Semi, title ? 50 : 44, GraphicsUnit.Pixel))
                using (var br = new SolidBrush(Color.FromArgb(246, 246, 250)))
                    g.DrawString("Rectangle for PC", f, br, new RectangleF(0, y, W, 70), center);
                y += title ? 72 : 64;
                using (var f = new Font(Sans, title ? 19 : 18, GraphicsUnit.Pixel))
                using (var br = new SolidBrush(Color.FromArgb(178, 178, 194)))
                    g.DrawString(title ? "Keyboard shortcuts and snap areas for Windows" : "40 window actions  ·  Snap areas  ·  6 languages", f, br, new RectangleF(0, y, W, 30), center);
                y += 40;
                using (var f = new Font(Sans, 14.5f, GraphicsUnit.Pixel))
                using (var br = new SolidBrush(title ? Color.FromArgb(120, 120, 138) : Color.FromArgb(140, 165, 255)))
                    g.DrawString(title ? "Inspired by Rectangle for macOS  ·  Free and open source" : "github.com/kkkometo/rectangle-for-pc", f, br, new RectangleF(0, y, W, 24), center);
            }
            return b;
        }

        static void DrawFaded(Graphics g, Bitmap b, double a)
        {
            if (a <= 0) return;
            if (a >= 1) { g.DrawImageUnscaled(b, 0, 0); return; }
            var cm = new ColorMatrix { Matrix33 = (float)a };
            using (var ia = new ImageAttributes())
            {
                ia.SetColorMatrix(cm);
                g.DrawImage(b, new Rectangle(0, 0, b.Width, b.Height), 0, 0, b.Width, b.Height, GraphicsUnit.Pixel, ia);
            }
        }

        Bitmap scratch;

        public Bitmap Render(double t)
        {
            double end = Story.Duration;
            double titleA = t < 2.4 ? 1 : t < 2.8 ? 1 - Ease.InOutCubic((t - 2.4) / 0.4) : t > end - 0.4 ? Ease.InOutCubic((t - (end - 0.4)) / 0.4) : 0;
            double explA = t < 2.4 ? 0 : t < 6.4 ? 1 : t < 6.8 ? 1 - Ease.InOutCubic((t - 6.4) / 0.4) : 0;
            double endA = t < 42.6 ? 0 : Ease.InOutCubic((t - 42.6) / 0.4);
            using (var g = Graphics.FromImage(frame))
            {
                Hq(g);
                if (titleA < 1 && explA < 1 && endA < 1) Stage(g, t);
                if (explA >= 1) Explainer(g, t);
                else if (explA > 0)
                {
                    if (scratch == null) scratch = new Bitmap(W, H, PixelFormat.Format32bppArgb);
                    using (var sg = Graphics.FromImage(scratch)) { Hq(sg); Explainer(sg, t); }
                    DrawFaded(g, scratch, explA);
                }
                DrawFaded(g, endCard, endA);
                DrawFaded(g, titleCard, titleA);
            }
            return frame;
        }

        // ---------- explicación: "Hold Win + Alt, then tap a key" ----------
        void Explainer(Graphics g, double t)
        {
            g.DrawImageUnscaled(backdrop, 0, 0);
            double ta = Ease.Clamp((t - 2.7) / 0.4);
            float dy = (float)((1 - Ease.OutCubic((t - 2.7) / 0.5)) * 12);
            using (var b = new SolidBrush(Color.FromArgb(Ease.A(ta, 255), 246, 246, 250)))
                g.DrawString("Hold Win + Alt, then tap a key", fExplain, b, new RectangleF(0, 206 + dy, W, 60), center);

            string[] keys = { "Win", "Alt", "←" };
            float[] widths = { 150, 136, 116 };
            const float kh = 88, plus = 60, arrowGap = 96, icon = 92, y = 308;
            double[] down = { 3.35, 3.75 };
            const double tap = 4.35, release = 5.5;
            double ka = Ease.Clamp((t - 2.9) / 0.35);
            float total = widths[0] + widths[1] + widths[2] + 2 * plus + arrowGap + icon;
            float x = (W - total) / 2;
            for (int k = 0; k < 3; k++)
            {
                if (k > 0)
                {
                    using (var b = new SolidBrush(Color.FromArgb(Ease.A(ka, 255), 120, 122, 140)))
                    {
                        var ps = g.MeasureString("+", fBigPlus, PointF.Empty, tight);
                        g.DrawString("+", fBigPlus, b, x + (plus - ps.Width) / 2, y + (kh - ps.Height) / 2, tight);
                    }
                    x += plus;
                }
                bool dn = k < 2 ? t >= down[k] && t < release : t >= tap && t < tap + 0.3;
                var face = new RectangleF(x, y, widths[k], kh);
                DrawKey(g, face, keys[k], fBigKey, dn, ka, 8, 2.2f);
                if (k == 2) Ripple(g, face, t - tap, ka, 22);
                x += widths[k];
            }
            // → resultado
            double ra = t >= tap ? Ease.Clamp((t - tap) / 0.2) : 0.0;
            using (var pen = new Pen(Color.FromArgb(Ease.A(ka, 150), 150, 152, 170), 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                float ax = x + 24, ay = y + kh / 2, aw = arrowGap - 48;
                g.DrawLine(pen, ax, ay, ax + aw, ay);
                g.DrawLines(pen, new[] { new PointF(ax + aw - 9, ay - 9), new PointF(ax + aw, ay), new PointF(ax + aw - 9, ay + 9) });
            }
            x += arrowGap;
            var ir = new RectangleF(x, y + (kh - icon) / 2, icon, icon);
            Icons.Draw(g, ActionId.LeftHalf, ir, Color.FromArgb(Ease.A(ka * (0.3 + 0.7 * ra), 255), 235, 236, 245));
            using (var b = new SolidBrush(Color.FromArgb(Ease.A(ra, 255), 214, 216, 228)))
            {
                var ls = g.MeasureString("Left Half", fExplainSub, PointF.Empty, tight);
                g.DrawString("Left Half", fExplainSub, b, ir.X + (icon - ls.Width) / 2, ir.Bottom + 6, tight);
            }

            double sa = Ease.Clamp((t - 5.0) / 0.4);
            using (var b = new SolidBrush(Color.FromArgb(Ease.A(sa, 255), 160, 162, 182)))
                g.DrawString("Arrows for halves   ·   U I J K for corners   ·   D F G for thirds", fExplainSub, b, new RectangleF(0, 470, W, 30), center);
        }

        static readonly Color Accent = Color.FromArgb(96, 140, 255);

        void DrawKey(Graphics g, RectangleF face0, string text, Font font, bool down, double a, float depth, float arrowScale)
        {
            float rad = face0.Height * 0.2f;
            if (down)
                for (int i = 3; i >= 1; i--)
                    using (var p = Theme.Rounded(RectangleF.Inflate(face0, 3 * i, 3 * i), rad + 3 * i))
                    using (var b = new SolidBrush(Color.FromArgb(Ease.A(a, 22), Accent))) g.FillPath(b, p);
            var edge = new RectangleF(face0.X, face0.Y + depth, face0.Width, face0.Height);
            using (var p = Theme.Rounded(edge, rad))
            using (var b = new SolidBrush(down ? Color.FromArgb(Ease.A(a, 255), 38, 66, 168) : Color.FromArgb(Ease.A(a, 255), 118, 120, 136))) g.FillPath(b, p);
            var face = down ? new RectangleF(face0.X, face0.Y + depth * 0.75f, face0.Width, face0.Height) : face0;
            using (var p = Theme.Rounded(face, rad))
            {
                Color c1 = down ? Color.FromArgb(Ease.A(a, 255), 122, 160, 255) : Color.FromArgb(Ease.A(a, 255), 252, 252, 254);
                Color c2 = down ? Color.FromArgb(Ease.A(a, 255), 74, 112, 242) : Color.FromArgb(Ease.A(a, 255), 220, 221, 230);
                using (var b = new LinearGradientBrush(new RectangleF(face.X, face.Y - 1, face.Width, face.Height + 2), c1, c2, 90f)) g.FillPath(b, p);
                using (var pen = new Pen(Color.FromArgb(Ease.A(a, down ? 110 : 80), 255, 255, 255), 1f)) g.DrawPath(pen, p);
            }
            var ink = down ? Color.FromArgb(Ease.A(a, 255), 255, 255, 255) : Color.FromArgb(Ease.A(a, 255), 26, 26, 34);
            float cx = face.X + face.Width / 2, cy = face.Y + face.Height / 2;
            if (!ArrowKey(g, text, cx, cy, ink, arrowScale))
            {
                var ts = g.MeasureString(text, font, PointF.Empty, tight);
                using (var b = new SolidBrush(ink)) g.DrawString(text, font, b, cx - ts.Width / 2, cy - ts.Height / 2 - 1, tight);
            }
        }

        // anillo que se expande al tocar la tecla: "¡aquí se disparó la acción!"
        static void Ripple(Graphics g, RectangleF face, double dt, double a, float grow)
        {
            double k = dt / 0.45;
            if (k < 0 || k >= 1) return;
            float inf = (float)(3 + grow * Ease.OutCubic(k));
            using (var p = Theme.Rounded(RectangleF.Inflate(face, inf, inf), face.Height * 0.2f + inf))
            using (var pen = new Pen(Color.FromArgb(Ease.A(a * (1 - k), 230), Accent), 3f)) g.DrawPath(pen, p);
        }

        void Stage(Graphics g, double t)
        {
            g.DrawImageUnscaled(stageBase, 0, 0);

            var st = g.Save();
            g.TranslateTransform(Scr.X, Scr.Y);
            using (var clip = Theme.Rounded(new RectangleF(0, 0, Scr.Width, Scr.Height), 10)) g.SetClip(clip);

            var order = new List<AppWin>(s.Wins);
            order.Sort(delegate(AppWin a, AppWin b) { return a.LastRaise(t).CompareTo(b.LastRaise(t)); });
            AppWin active = order[order.Count - 1];
            foreach (var w in order) DrawWindow(g, w, w.At(t), w == active, t);

            foreach (var fl in s.Flashes) DrawFlash(g, fl, t);
            foreach (var b in s.Badges) DrawBadge(g, b, t);
            foreach (var f in s.Feet) DrawFootprint(g, f, t);

            g.DrawImageUnscaled(taskbar, 0, WorkH);
            for (int i = 0; i < s.Wins.Count; i++)
            {
                bool act = s.Wins[i] == active;
                float x = TaskX(i) + 9, y = WorkH + 27;
                float iw = act ? 10 : 4;
                using (var p = Theme.Rounded(new RectangleF(x - iw / 2, y, iw, 2.5f), 1.25f))
                using (var b = new SolidBrush(act ? s.Wins[i].Accent : Color.FromArgb(150, 160, 160, 170))) g.FillPath(b, p);
            }

            DrawCursor(g, t);
            g.Restore(st);

            DrawCaption(g, t);
            DrawHud(g, t);
        }

        // ---------- ventanas ----------
        static Color Body(int kind)
        {
            switch (kind)
            {
                case 0: return Color.FromArgb(32, 34, 40);
                case 1: return Color.FromArgb(37, 34, 30);
                case 2: return Color.FromArgb(14, 16, 21);
                default: return Color.FromArgb(29, 29, 36);
            }
        }

        static double Hash(int a, int b)
        {
            unchecked
            {
                uint x = (uint)(a * 73856093) ^ (uint)(b * 19349663);
                x ^= x >> 13; x *= 0x5bd1e995; x ^= x >> 15;
                return (x & 0xFFFF) / 65535.0;
            }
        }

        static void Bar(Graphics g, Color c, float x, float y, float w, float h)
        {
            if (w <= 1) return;
            using (var p = Theme.Rounded(new RectangleF(x, y, w, h), h / 2))
            using (var b = new SolidBrush(c)) g.FillPath(b, p);
        }

        void DrawWindow(Graphics g, AppWin w, RectangleF r, bool active, double t)
        {
            if (r.Width < 8 || r.Height < 8) return;
            const float rad = 7f;
            for (int i = 7; i >= 1; i--)
                using (var p = Theme.Rounded(new RectangleF(r.X - i, r.Y - i + 4, r.Width + 2 * i, r.Height + 2 * i), rad + i))
                using (var b = new SolidBrush(Color.FromArgb(active ? 15 : 10, 0, 0, 0))) g.FillPath(b, p);

            using (var body = Theme.Rounded(r, rad))
            {
                using (var b = new SolidBrush(Body(w.Kind))) g.FillPath(b, body);
                var st = g.Save();
                g.SetClip(body, CombineMode.Intersect);

                var tb = new RectangleF(r.X, r.Y, r.Width, 26);
                using (var b = new SolidBrush(active ? Color.FromArgb(46, 48, 55) : Color.FromArgb(35, 37, 42))) g.FillRectangle(b, tb);
                var ic = new RectangleF(r.X + 9, r.Y + 7, 12, 12);
                using (var p = Theme.Rounded(ic, 3.5f))
                using (var b = new SolidBrush(active ? w.Accent : Color.FromArgb(150, w.Accent))) g.FillPath(b, p);
                using (var b = new SolidBrush(active ? Color.FromArgb(235, 235, 240) : Color.FromArgb(140, 140, 150)))
                    g.DrawString(w.Title, fWinTitle, b, r.X + 27, r.Y + 6);
                using (var pen = new Pen(active ? Color.FromArgb(200, 200, 205) : Color.FromArgb(110, 110, 118), 1f))
                {
                    float cy = r.Y + 13;
                    float x1 = r.Right - 18;
                    g.DrawLine(pen, x1 - 4, cy - 4, x1 + 4, cy + 4); g.DrawLine(pen, x1 - 4, cy + 4, x1 + 4, cy - 4);
                    float x2 = r.Right - 44;
                    g.DrawRectangle(pen, x2 - 4, cy - 4, 8, 8);
                    float x3 = r.Right - 70;
                    g.DrawLine(pen, x3 - 4, cy, x3 + 4, cy);
                }

                var c = new RectangleF(r.X, r.Y + 26, r.Width, r.Height - 26);
                switch (w.Kind)
                {
                    case 0: BrowserContent(g, c, w.Accent); break;
                    case 1: NotesContent(g, c, w.Accent); break;
                    case 2: TerminalContent(g, c, t); break;
                    default: PhotosContent(g, c); break;
                }
                g.Restore(st);
                using (var pen = new Pen(Color.FromArgb(active ? 64 : 34, 255, 255, 255), 1f)) g.DrawPath(pen, body);
            }
        }

        void BrowserContent(Graphics g, RectangleF c, Color accent)
        {
            using (var b = new SolidBrush(Color.FromArgb(40, 42, 49))) g.FillRectangle(b, c.X, c.Y, c.Width, 24);
            for (int i = 0; i < 3; i++)
                using (var b = new SolidBrush(Color.FromArgb(90, 92, 102))) g.FillEllipse(b, c.X + 10 + i * 12, c.Y + 9, 6, 6);
            Bar(g, Color.FromArgb(58, 61, 70), c.X + 50, c.Y + 5, c.Width - 64, 14);
            float hx = c.X + 16, hy = c.Y + 38, hw = c.Width - 32, hh = Math.Min(92, c.Height * 0.3f);
            if (hw > 10 && hh > 10)
            {
                var hero = new RectangleF(hx, hy, hw, hh);
                using (var p = Theme.Rounded(hero, 6))
                using (var b = new LinearGradientBrush(hero, accent, Color.FromArgb(150, 90, 230), 20f)) g.FillPath(b, p);
                Bar(g, Color.FromArgb(220, 255, 255, 255), hx + 14, hy + hh / 2 - 9, Math.Min(150, hw * 0.45f), 8);
                Bar(g, Color.FromArgb(140, 255, 255, 255), hx + 14, hy + hh / 2 + 5, Math.Min(100, hw * 0.3f), 6);
            }
            float y = hy + hh + 16;
            Bar(g, Color.FromArgb(200, 210, 212, 220), hx, y, Math.Min(200, hw * 0.55f), 9);
            y += 22;
            for (int i = 0; y < c.Bottom - 8; i++, y += 13)
                Bar(g, Color.FromArgb(80, 200, 205, 215), hx, y, (float)(hw * (0.5 + 0.45 * Hash(1, i))), 5);
        }

        void NotesContent(Graphics g, RectangleF c, Color accent)
        {
            float x = c.X + 16;
            if (c.Width > 280)
            {
                using (var b = new SolidBrush(Color.FromArgb(31, 29, 26))) g.FillRectangle(b, c.X, c.Y, 92, c.Height);
                for (int i = 0; i < 9 && c.Y + 16 + i * 20 < c.Bottom; i++)
                    Bar(g, i == 1 ? Color.FromArgb(200, accent) : Color.FromArgb(70, 230, 220, 205), c.X + 12, c.Y + 16 + i * 20, (float)(44 + 26 * Hash(2, i)), 6);
                x = c.X + 108;
            }
            float w = c.Right - x - 16;
            float y = c.Y + 18;
            Bar(g, Color.FromArgb(230, accent), x, y, Math.Min(180, w * 0.6f), 10);
            y += 26;
            for (int i = 0; y < c.Bottom - 8; i++, y += 14)
            {
                bool gap = i % 6 == 5;
                if (!gap) Bar(g, Color.FromArgb(90, 235, 228, 215), x, y, (float)(w * (0.55 + 0.42 * Hash(3, i))), 5);
            }
        }

        void TerminalContent(Graphics g, RectangleF c, double t)
        {
            float y = c.Y + 12;
            int i = 0;
            for (; y < c.Bottom - 16; i++, y += 15)
            {
                int kind = (int)(Hash(4, i) * 4);
                float x = c.X + 12;
                if (kind == 0) { Bar(g, Color.FromArgb(220, 80, 205, 125), x, y, 6, 6); x += 12; }
                float wbar = (float)((c.Width - 40) * (0.25 + 0.5 * Hash(5, i)));
                Color col = kind == 1 ? Color.FromArgb(150, 120, 170, 255) : kind == 2 ? Color.FromArgb(150, 80, 205, 125) : Color.FromArgb(110, 210, 215, 225);
                Bar(g, col, x, y, wbar, 6);
            }
            if (((int)(t * 2)) % 2 == 0)
                using (var b = new SolidBrush(Color.FromArgb(230, 80, 205, 125))) g.FillRectangle(b, c.X + 12, y - 2, 7, 11);
        }

        void PhotosContent(Graphics g, RectangleF c)
        {
            Bar(g, Color.FromArgb(200, 230, 230, 240), c.X + 12, c.Y + 12, 70, 8);
            Color[][] pal = {
                new[] { Color.FromArgb(255, 150, 120), Color.FromArgb(200, 80, 160) },
                new[] { Color.FromArgb(90, 170, 255), Color.FromArgb(70, 80, 200) },
                new[] { Color.FromArgb(120, 220, 170), Color.FromArgb(40, 140, 140) },
                new[] { Color.FromArgb(255, 210, 110), Color.FromArgb(230, 120, 60) },
                new[] { Color.FromArgb(190, 150, 255), Color.FromArgb(110, 70, 200) },
            };
            const float size = 58, gap = 6;
            int n = 0;
            for (float y = c.Y + 32; y + size < c.Bottom + 30; y += size + gap)
                for (float x = c.X + 12; x + size < c.Right + 30; x += size + gap, n++)
                {
                    var r = new RectangleF(x, y, size, size);
                    var p2 = pal[(int)(Hash(6, n) * pal.Length) % pal.Length];
                    using (var p = Theme.Rounded(r, 5))
                    using (var b = new LinearGradientBrush(r, p2[0], p2[1], 50f)) g.FillPath(b, p);
                }
        }

        // ---------- capas ----------
        void DrawBadge(Graphics g, Badge bd, double t)
        {
            double a = Ease.Window(t, bd.T + 0.08, bd.T + 0.95, 0.12, 0.3);
            if (a <= 0) return;
            var r = bd.W.At(t);
            var sz = g.MeasureString(bd.Text, fBadge, PointF.Empty, tight);
            float bw = sz.Width + 40, bh = 64;
            var box = new RectangleF(r.X + (r.Width - bw) / 2, r.Y + (r.Height - bh) / 2, bw, bh);
            using (var p = Theme.Rounded(box, 14))
            {
                using (var b = new SolidBrush(Color.FromArgb(Ease.A(a, 175), 10, 10, 14))) g.FillPath(b, p);
                using (var pen = new Pen(Color.FromArgb(Ease.A(a, 60), 255, 255, 255))) g.DrawPath(pen, p);
            }
            using (var b = new SolidBrush(Color.FromArgb(Ease.A(a, 255), 255, 255, 255)))
                g.DrawString(bd.Text, fBadge, b, box.X + 20, box.Y + (bh - sz.Height) / 2 - 2, tight);
        }

        static void DrawFootprint(Graphics g, Foot f, double t)
        {
            if (t < f.T0 || t >= f.T1) return;
            double a = Ease.Clamp((t - f.T0) / 0.12);
            var r = new RectangleF(f.R.X + 1.5f, f.R.Y + 1.5f, f.R.Width - 3, f.R.Height - 3);
            using (var path = Theme.Rounded(r, 10))
            {
                using (var b = new SolidBrush(Color.FromArgb(Ease.A(a, 78), 150, 150, 156))) g.FillPath(b, path);
                using (var pen = new Pen(Color.FromArgb(Ease.A(a, 70), 0, 0, 0), 3f)) g.DrawPath(pen, path);
                using (var pen = new Pen(Color.FromArgb(Ease.A(a, 150), 255, 255, 255), 1.5f)) g.DrawPath(pen, path);
            }
        }

        void DrawCursor(Graphics g, double t)
        {
            double a = 0;
            foreach (var v in s.CursorVisible) a = Math.Max(a, Ease.Window(t, v[0], v[1], 0.2, 0.2));
            if (a <= 0) return;
            var p = s.CursorAt(t);
            foreach (var c in s.Clicks)
            {
                double k = (t - c) / 0.4;
                if (k < 0 || k > 1) continue;
                float rr = (float)(5 + 16 * Ease.OutCubic(k));
                using (var pen = new Pen(Color.FromArgb(Ease.A(a * (1 - k), 200), 255, 255, 255), 2f)) g.DrawEllipse(pen, p.X - rr, p.Y - rr, 2 * rr, 2 * rr);
            }
            PointF[] arrow = { new PointF(0, 0), new PointF(0, 17), new PointF(4.2f, 13), new PointF(7.2f, 19.5f), new PointF(9.6f, 18.4f), new PointF(6.7f, 12.1f), new PointF(12.2f, 12.1f) };
            for (int i = 0; i < arrow.Length; i++) arrow[i] = new PointF(p.X + arrow[i].X, p.Y + arrow[i].Y);
            using (var b = new SolidBrush(Color.FromArgb(Ease.A(a, 255), 255, 255, 255))) g.FillPolygon(b, arrow);
            using (var pen = new Pen(Color.FromArgb(Ease.A(a, 255), 10, 10, 10), 1.2f) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, arrow);
        }

        static void DrawFlash(Graphics g, Flash f, double t)
        {
            double k = (t - f.T) / 0.6;
            if (k < 0 || k >= 1) return;
            double a = 1 - Ease.OutCubic(k);
            var r = f.W.At(t);
            using (var p = Theme.Rounded(RectangleF.Inflate(r, 2, 2), 9))
            {
                using (var pen = new Pen(Color.FromArgb(Ease.A(a, 70), Accent), 9f)) g.DrawPath(pen, p);
                using (var pen = new Pen(Color.FromArgb(Ease.A(a, 240), Accent), 2.5f)) g.DrawPath(pen, p);
            }
        }

        void DrawCaption(Graphics g, double t)
        {
            foreach (var c in s.Captions)
            {
                double a = Ease.Window(t, c.T0, c.T1, 0.35, 0.25);
                if (a <= 0) continue;
                float dy = (float)((1 - Ease.OutCubic((t - c.T0) / 0.45)) * 10);
                using (var b = new SolidBrush(Color.FromArgb(Ease.A(a, 255), 246, 246, 250)))
                    g.DrawString(c.Title, fCapTitle, b, new RectangleF(0, 9 + dy, W, 40), center);
                using (var b = new SolidBrush(Color.FromArgb(Ease.A(a, 255), 160, 162, 182)))
                    g.DrawString(c.Sub, fCapSub, b, new RectangleF(0, 46 + dy, W, 26), center);
            }
        }

        // ---------- teclas pulsadas (franja inferior) ----------
        const float PlusW = 36, LabelGap = 40, IconSz = 34;

        void DrawHud(Graphics g, double t)
        {
            for (int i = 0; i < s.Shots.Count; i++)
            {
                var sh = s.Shots[i];
                if (t < sh.T0 || t >= sh.T1) continue;
                bool joinedBefore = i > 0 && Math.Abs(s.Shots[i - 1].T1 - sh.T0) < 1e-6;
                bool joinedAfter = i < s.Shots.Count - 1 && Math.Abs(s.Shots[i + 1].T0 - sh.T1) < 1e-6;
                double a = Ease.Window(t, sh.T0, sh.T1, joinedBefore ? 0 : 0.18, joinedAfter ? 0 : 0.22);
                DrawShot(g, sh, a, t);
            }
        }

        float KeyWidth(Graphics g, string key)
        {
            if (key == "←" || key == "→" || key == "↑" || key == "↓") return KeyH + 14;
            return Math.Max(KeyH + 10, g.MeasureString(key, fKey, PointF.Empty, tight).Width + 44);
        }

        float LabelWidth(Graphics g, string label)
        {
            int dot = label.IndexOf(" · ");
            string main = dot >= 0 ? label.Substring(0, dot) : label;
            return IconSz + 14 + g.MeasureString(main, fLabel, PointF.Empty, tight).Width + (dot >= 0 ? 72 : 0);
        }

        void DrawShot(Graphics g, Shot sh, double a, double t)
        {
            int tap = -1;
            for (int j = 0; j < sh.Taps.Length; j++) if (t >= sh.Taps[j]) tap = j;
            int nk = sh.Keys.Length;
            var widths = new float[nk];
            float keysW = 0;
            for (int k = 0; k < nk; k++) { widths[k] = KeyWidth(g, sh.Keys[k]); keysW += widths[k] + (k > 0 ? PlusW : 0); }
            float labelW = 0;
            foreach (var l in sh.Labels) labelW = Math.Max(labelW, LabelWidth(g, l)); // así las teclas no saltan
            float gap = nk > 0 ? LabelGap : 0;
            float x = (W - (keysW + gap + labelW)) / 2f;

            for (int k = 0; k < nk; k++)
            {
                if (k > 0)
                {
                    var ps = g.MeasureString("+", fPlus, PointF.Empty, tight);
                    using (var b = new SolidBrush(Color.FromArgb(Ease.A(a, 255), 120, 122, 140)))
                        g.DrawString("+", fPlus, b, x + (PlusW - ps.Width) / 2, HudTop + (KeyH - ps.Height) / 2, tight);
                    x += PlusW;
                }
                bool down = k < nk - 1
                    ? t >= sh.Down[k] && t < sh.Release
                    : tap >= 0 && t < sh.Taps[tap] + 0.24;
                var face = new RectangleF(x, HudTop, widths[k], KeyH);
                DrawKey(g, face, sh.Keys[k], fKey, down, a, 6, 1.5f);
                if (k == nk - 1 && tap >= 0) Ripple(g, face, t - sh.Taps[tap], a, 16);
                x += widths[k];
            }
            x += gap;
            if (tap < 0) return;
            double la = Math.Min(a, Ease.Clamp((t - sh.Taps[tap]) / 0.15));
            string main = sh.Labels[tap], extra = null;
            int dot = main.IndexOf(" · ");
            if (dot >= 0) { extra = main.Substring(dot + 3); main = main.Substring(0, dot); }
            Icons.Draw(g, sh.Icons[tap], new RectangleF(x, HudTop + (KeyH - IconSz) / 2, IconSz, IconSz), Color.FromArgb(Ease.A(la, 240), 235, 236, 245));
            var ms = g.MeasureString(main, fLabel, PointF.Empty, tight);
            float tx = x + IconSz + 14;
            using (var b = new SolidBrush(Color.FromArgb(Ease.A(la, 255), 228, 229, 238)))
            {
                g.DrawString(main, fLabel, b, tx, HudTop + (KeyH - ms.Height) / 2 - 1, tight);
                if (extra != null)
                {
                    float mx = tx + ms.Width + 14, pw = 58, ph = 44;
                    var pr = new RectangleF(mx, HudTop + (KeyH - ph) / 2, pw, ph);
                    using (var pill = Theme.Rounded(pr, 10))
                    using (var pb = new SolidBrush(Color.FromArgb(Ease.A(la, 255), Accent))) g.FillPath(pb, pill);
                    var fs = g.MeasureString(extra, fFrac, PointF.Empty, tight);
                    using (var wb = new SolidBrush(Color.FromArgb(Ease.A(la, 255), 255, 255, 255)))
                        g.DrawString(extra, fFrac, wb, pr.X + (pw - fs.Width) / 2, pr.Y + (ph - fs.Height) / 2 - 1, tight);
                }
            }
        }

        // ← → ↑ ↓ como vectores: los glifos de Segoe UI salen demasiado pequeños dentro de una tecla
        static bool ArrowKey(Graphics g, string key, float cx, float cy, Color c, float scale)
        {
            float dx, dy;
            switch (key)
            {
                case "←": dx = -1; dy = 0; break;
                case "→": dx = 1; dy = 0; break;
                case "↑": dx = 0; dy = -1; break;
                case "↓": dx = 0; dy = 1; break;
                default: return false;
            }
            float len = 7.5f * scale, head = 5.5f * scale;
            using (var pen = new Pen(c, 2.4f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                float tx = cx + dx * len, ty = cy + dy * len;
                g.DrawLine(pen, cx - dx * len, cy - dy * len, tx, ty);
                g.DrawLines(pen, new[] {
                    new PointF(tx - dx * head - dy * head, ty - dy * head + dx * head),
                    new PointF(tx, ty),
                    new PointF(tx - dx * head + dy * head, ty - dy * head - dx * head) });
            }
            return true;
        }
    }

    static class DemoProgram
    {
        const int Fps = 25;

        static int Main(string[] args)
        {
            Lang.Set("en");
            string dir = args.Length > 0 ? args[0] : "frames";
            Directory.CreateDirectory(dir);
            var story = new Story();
            var r = new Renderer(story);
            if (args.Length > 2 && args[1] == "--only")
            {
                // vista previa: solo los instantes pedidos (segundos separados por comas)
                foreach (var s in args[2].Split(','))
                    r.Render(double.Parse(s, System.Globalization.CultureInfo.InvariantCulture))
                     .Save(Path.Combine(dir, "t_" + s + ".png"), ImageFormat.Png);
                return 0;
            }
            foreach (var f in Directory.GetFiles(dir, "frame_*.png")) File.Delete(f);
            int n = (int)Math.Round(Story.Duration * Fps);
            for (int i = 0; i < n; i++)
            {
                var b = r.Render(i / (double)Fps);
                b.Save(Path.Combine(dir, "frame_" + i.ToString("D5") + ".png"), ImageFormat.Png);
            }
            Console.WriteLine(n + " fotogramas en " + dir);
            return 0;
        }
    }
}
