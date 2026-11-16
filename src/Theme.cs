using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Encuadre
{
    public sealed class Palette
    {
        public bool Light;
        public Color Back, Surface, SurfaceHover, Border, Text, SubText, Disabled, Accent, AccentText, Sep, Field, FieldBorder, MenuBack, MenuHover;

        public static Palette Create(bool light)
        {
            var p = new Palette { Light = light };
            Color acc = Theme.AccentColor(light);
            if (light)
            {
                p.Back = Color.FromArgb(243, 243, 243);
                p.Surface = Color.FromArgb(251, 251, 251);
                p.SurfaceHover = Color.FromArgb(240, 240, 240);
                p.Border = Color.FromArgb(229, 229, 229);
                p.Text = Color.FromArgb(26, 26, 26);
                p.SubText = Color.FromArgb(96, 96, 96);
                p.Disabled = Color.FromArgb(160, 160, 160);
                p.Sep = Color.FromArgb(225, 225, 225);
                p.Field = Color.FromArgb(255, 255, 255);
                p.FieldBorder = Color.FromArgb(214, 214, 214);
                p.MenuBack = Color.FromArgb(249, 249, 249);
                p.MenuHover = Color.FromArgb(233, 233, 233);
                p.AccentText = Color.White;
            }
            else
            {
                p.Back = Color.FromArgb(32, 32, 32);
                p.Surface = Color.FromArgb(43, 43, 43);
                p.SurfaceHover = Color.FromArgb(52, 52, 52);
                p.Border = Color.FromArgb(58, 58, 58);
                p.Text = Color.FromArgb(255, 255, 255);
                p.SubText = Color.FromArgb(170, 170, 170);
                p.Disabled = Color.FromArgb(110, 110, 110);
                p.Sep = Color.FromArgb(64, 64, 64);
                p.Field = Color.FromArgb(50, 50, 50);
                p.FieldBorder = Color.FromArgb(72, 72, 72);
                p.MenuBack = Color.FromArgb(44, 44, 44);
                p.MenuHover = Color.FromArgb(61, 61, 61);
                p.AccentText = Color.Black;
            }
            p.Accent = acc;
            return p;
        }
    }

    public static class Theme
    {
        public static string Force; // "dark" / "light" para pruebas
        static float scale = -1;

        public static float Scale
        {
            get
            {
                if (scale < 0)
                {
                    IntPtr dc = Native.GetDC(IntPtr.Zero);
                    int dpi = Native.GetDeviceCaps(dc, 88);
                    Native.ReleaseDC(IntPtr.Zero, dc);
                    scale = dpi > 0 ? dpi / 96f : 1f;
                }
                return scale;
            }
        }

        public static int S(float v) { return (int)Math.Round(v * Scale); }

        static int ReadDword(string key, string name, int def)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(key))
                {
                    if (k == null) return def;
                    var v = k.GetValue(name);
                    return v is int ? (int)v : def;
                }
            }
            catch { return def; }
        }

        const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        public static bool SystemLight
        {
            get
            {
                if (Force != null) return Force == "light";
                return ReadDword(PersonalizeKey, "SystemUsesLightTheme", 0) == 1;
            }
        }

        public static bool AppsLight
        {
            get
            {
                if (Force != null) return Force == "light";
                return ReadDword(PersonalizeKey, "AppsUseLightTheme", 1) == 1;
            }
        }

        public static Color AccentColor(bool light)
        {
            int v = ReadDword(@"Software\Microsoft\Windows\DWM", "AccentColor", unchecked((int)0xFFD47800));
            Color c = Color.FromArgb(v & 0xFF, (v >> 8) & 0xFF, (v >> 16) & 0xFF); // ABGR
            if (!light)
            {
                // en modo oscuro Windows usa una versión más clara del acento
                c = Color.FromArgb((c.R * 3 + 255 * 2) / 5, (c.G * 3 + 255 * 2) / 5, (c.B * 3 + 255 * 2) / 5);
            }
            return c;
        }

        public static bool WindowsSnapEnabled
        {
            get
            {
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
                    {
                        var v = k == null ? null : k.GetValue("WindowArrangementActive") as string;
                        return v != "0";
                    }
                }
                catch { return false; }
            }
        }

        public static GraphicsPath Rounded(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Font UiFont(float pt) { return new Font("Segoe UI", pt, FontStyle.Regular, GraphicsUnit.Point); }
        public static Font UiFont(float pt, FontStyle st) { return new Font("Segoe UI", pt, st, GraphicsUnit.Point); }
    }

    // Iconos de las acciones: una pantalla con la zona que ocupará la ventana (estilo Rectangle)
    public static class Icons
    {
        public static Bitmap Action(ActionId a, int size, Color c)
        {
            var b = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                Draw(g, a, new RectangleF(0, 0, size, size), c);
            }
            return b;
        }

        public static void Draw(Graphics g, ActionId a, RectangleF box, Color c)
        {
            float s = box.Width;
            float stroke = Math.Max(1f, s / 13f);
            // pantalla apaisada
            float sw = s * 0.94f, sh = s * 0.72f;
            var screen = new RectangleF(box.X + (s - sw) / 2, box.Y + (s - sh) / 2, sw, sh);
            float rad = s * 0.12f;
            using (var pen = new Pen(c, stroke))
            using (var path = Theme.Rounded(new RectangleF(screen.X + stroke / 2, screen.Y + stroke / 2, screen.Width - stroke, screen.Height - stroke), rad))
                g.DrawPath(pen, path);
            float pad = stroke + Math.Max(1f, s / 16f);
            var inner = new RectangleF(screen.X + pad, screen.Y + pad, screen.Width - 2 * pad, screen.Height - 2 * pad);

            using (var br = new SolidBrush(c))
            using (var pen = new Pen(c, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                switch (a)
                {
                    case ActionId.AlmostMaximize: Fill(g, br, inner, 0.12f, 0.15f, 0.88f, 0.85f); return;
                    case ActionId.MaximizeHeight: Fill(g, br, inner, 0.3f, 0, 0.7f, 1); return;
                    case ActionId.Center: Fill(g, br, inner, 0.25f, 0.2f, 0.75f, 0.8f); return;
                    case ActionId.Smaller:
                        Arrow(g, pen, inner, 0.0f, 0.0f, 0.38f, 0.38f);
                        Arrow(g, pen, inner, 1f, 1f, 0.62f, 0.62f);
                        return;
                    case ActionId.Larger:
                        Arrow(g, pen, inner, 0.38f, 0.38f, 0.0f, 0.0f);
                        Arrow(g, pen, inner, 0.62f, 0.62f, 1f, 1f);
                        return;
                    case ActionId.Restore:
                    {
                        float cx = inner.X + inner.Width / 2, cy = inner.Y + inner.Height / 2, r = Math.Min(inner.Width, inner.Height) * 0.42f;
                        g.DrawArc(pen, cx - r, cy - r, 2 * r, 2 * r, -60, 290);
                        float ax = cx + r * (float)Math.Cos(-60 * Math.PI / 180), ay = cy + r * (float)Math.Sin(-60 * Math.PI / 180);
                        float hs = r * 0.75f;
                        g.DrawLine(pen, ax, ay, ax - hs, ay);
                        g.DrawLine(pen, ax, ay, ax + hs * 0.15f, ay + hs * 0.9f);
                        return;
                    }
                    case ActionId.NextDisplay:
                    case ActionId.PreviousDisplay:
                    {
                        bool next = a == ActionId.NextDisplay;
                        float y = inner.Y + inner.Height / 2;
                        float x0 = inner.X + inner.Width * (next ? 0.15f : 0.85f), x1 = inner.X + inner.Width * (next ? 0.85f : 0.15f);
                        float hs = inner.Height * 0.35f * (next ? 1 : -1);
                        g.DrawLine(pen, x0, y, x1, y);
                        g.DrawLine(pen, x1, y, x1 - hs, y - Math.Abs(hs));
                        g.DrawLine(pen, x1, y, x1 - hs, y + Math.Abs(hs));
                        return;
                    }
                    case ActionId.MoveLeft: Fill(g, br, inner, 0, 0.2f, 0.45f, 0.8f); return;
                    case ActionId.MoveRight: Fill(g, br, inner, 0.55f, 0.2f, 1, 0.8f); return;
                    case ActionId.MoveUp: Fill(g, br, inner, 0.25f, 0, 0.75f, 0.5f); return;
                    case ActionId.MoveDown: Fill(g, br, inner, 0.25f, 0.5f, 0.75f, 1); return;
                }
                // acciones de mosaico: usar la misma geometría que la ventana real
                var unit = new RECT(0, 0, 1000, 1000);
                RECT t = Layout.Tile(a, unit, false, 0);
                float gapx = inner.Width * 0.06f, gapy = inner.Height * 0.08f;
                float x0f = t.Left / 1000f, y0f = t.Top / 1000f, x1f = t.Right / 1000f, y1f = t.Bottom / 1000f;
                var rr = new RectangleF(inner.X + inner.Width * x0f, inner.Y + inner.Height * y0f, inner.Width * (x1f - x0f), inner.Height * (y1f - y0f));
                if (x0f > 0) { rr.X += gapx / 2; rr.Width -= gapx / 2; }
                if (x1f < 1) rr.Width -= gapx / 2;
                if (y0f > 0) { rr.Y += gapy / 2; rr.Height -= gapy / 2; }
                if (y1f < 1) rr.Height -= gapy / 2;
                using (var p = Theme.Rounded(rr, s * 0.05f)) g.FillPath(br, p);
            }
        }

        static void Fill(Graphics g, Brush br, RectangleF inner, float x0, float y0, float x1, float y1)
        {
            var r = new RectangleF(inner.X + inner.Width * x0, inner.Y + inner.Height * y0, inner.Width * (x1 - x0), inner.Height * (y1 - y0));
            using (var p = Theme.Rounded(r, inner.Width * 0.06f)) g.FillPath(br, p);
        }

        static void Arrow(Graphics g, Pen pen, RectangleF inner, float fx, float fy, float tx, float ty)
        {
            float x0 = inner.X + inner.Width * fx, y0 = inner.Y + inner.Height * fy;
            float x1 = inner.X + inner.Width * tx, y1 = inner.Y + inner.Height * ty;
            g.DrawLine(pen, x0, y0, x1, y1);
            float dx = x1 - x0, dy = y1 - y0, len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.01f) return;
            dx /= len; dy /= len;
            float hs = Math.Min(inner.Width, inner.Height) * 0.28f;
            // dos alas de la flecha a ±45°
            float c = 0.7071f;
            g.DrawLine(pen, x1, y1, x1 - hs * (dx * c - dy * c), y1 - hs * (dy * c + dx * c));
            g.DrawLine(pen, x1, y1, x1 - hs * (dx * c + dy * c), y1 - hs * (dy * c - dx * c));
        }

        public static int TrayVariant = 0; // 0 = monocromo (blanco/negro según la barra de tareas)

        // Icono de la bandeja, dibujado píxel a píxel para que se vea nítido a 16 px
        public static Icon Tray(int size, bool lightTaskbar)
        {
            using (var b = TrayBitmap(size, lightTaskbar, TrayVariant))
            {
                IntPtr hi = b.GetHicon();
                var ic = (Icon)Icon.FromHandle(hi).Clone();
                Native.DestroyIcon(hi);
                return ic;
            }
        }

        // Tres bloques: mitad izquierda + dos cuartos a la derecha (un mosaico de ventanas)
        static void LayoutBlocks(int s, int x0, int y0, int w, int h, int gap, out Rectangle left, out Rectangle rt, out Rectangle rb)
        {
            int lw = (w - gap) / 2, rw = w - gap - lw;
            int th = (h - gap) / 2, bh = h - gap - th;
            left = new Rectangle(x0, y0, lw, h);
            rt = new Rectangle(x0 + lw + gap, y0, rw, th);
            rb = new Rectangle(x0 + lw + gap, y0 + th + gap, rw, bh);
        }

        static void Block(Graphics g, Color c, Rectangle r, float rad)
        {
            using (var br = new SolidBrush(c))
            using (var p = Theme.Rounded(new RectangleF(r.X, r.Y, r.Width, r.Height), rad)) g.FillPath(br, p);
        }

        public static Bitmap TrayBitmap(int s, bool lightTaskbar, int variant)
        {
            var b = new Bitmap(s, s, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                Rectangle l, rt, rb;
                if (variant >= 2)
                {
                    // insignia: cuadrado redondeado con el mosaico en blanco (2 = morado, 3 = grafito neutro)
                    Color c0 = variant == 2 ? Color.FromArgb(255, 66, 112, 255) : Color.FromArgb(255, 72, 72, 76);
                    Color c1 = variant == 2 ? Color.FromArgb(255, 156, 78, 245) : Color.FromArgb(255, 28, 28, 30);
                    using (var path = Theme.Rounded(new RectangleF(0, 0, s, s), s * 0.25f))
                    using (var br = new LinearGradientBrush(new PointF(0, 0), new PointF(s, s), c0, c1))
                        g.FillPath(br, path);
                    int pad = (int)Math.Round(s * 0.19f);
                    int gap = Math.Max(1, (int)Math.Round(s / 14f));
                    int w = s - 2 * pad, h = (int)Math.Round(w * 0.82f);
                    LayoutBlocks(s, pad, (s - h) / 2, w, h, gap, out l, out rt, out rb);
                    float rad = Math.Max(0.8f, s / 20f);
                    Block(g, Color.White, l, rad);
                    Block(g, Color.FromArgb(215, 255, 255, 255), rt, rad);
                    Block(g, Color.FromArgb(215, 255, 255, 255), rb, rad);
                }
                else
                {
                    // monocromo como los iconos del sistema
                    Color c = lightTaskbar ? Color.FromArgb(30, 30, 30) : Color.White;
                    int m = Math.Max(1, s / 16);
                    int w = s - 2 * m, h = (int)Math.Round(s * 0.75f);
                    int gap = s < 24 ? 2 : Math.Max(2, s / 10);
                    LayoutBlocks(s, m, (s - h) / 2, w, h, gap, out l, out rt, out rb);
                    float rad = Math.Max(1f, s / 14f);
                    Block(g, c, l, rad);
                    Color side = variant == 1 ? Color.FromArgb(140, c) : c;
                    Block(g, side, rt, rad);
                    Block(g, side, rb, rad);
                }
            }
            return b;
        }

        // Logo de la app: cuadrado redondeado con degradado y una ventana partida
        // Logo de la app = mismo diseño que el icono de la bandeja (con un pequeño margen en tamaños grandes)
        public static Bitmap Logo(int size)
        {
            int m = size >= 48 ? (int)Math.Round(size * 0.06f) : 0;
            var b = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var inner = TrayBitmap(size - 2 * m, false, 3))
            using (var g = Graphics.FromImage(b))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImageUnscaled(inner, m, m);
            }
            return b;
        }

        public static Bitmap OldLogo(int size)
        {
            var b = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float m = size * 0.06f;
                var bg = new RectangleF(m, m, size - 2 * m, size - 2 * m);
                using (var path = Theme.Rounded(bg, size * 0.22f))
                using (var br = new LinearGradientBrush(new PointF(bg.Left, bg.Top), new PointF(bg.Right, bg.Bottom), Color.FromArgb(255, 74, 108, 247), Color.FromArgb(255, 150, 82, 240)))
                    g.FillPath(br, path);
                using (var path = Theme.Rounded(new RectangleF(bg.X, bg.Y, bg.Width, bg.Height * 0.5f), size * 0.22f))
                using (var sheen = new SolidBrush(Color.FromArgb(28, 255, 255, 255)))
                    g.FillPath(sheen, path);

                float st = Math.Max(1.5f, size * 0.055f);
                float w = size * 0.62f, h = size * 0.46f;
                var scr = new RectangleF((size - w) / 2, (size - h) / 2, w, h);
                using (var pen = new Pen(Color.White, st))
                using (var p = Theme.Rounded(scr, size * 0.07f)) g.DrawPath(pen, p);
                float pad = st * 1.25f;
                var left = new RectangleF(scr.X + pad, scr.Y + pad, scr.Width / 2 - pad * 1.25f, scr.Height - 2 * pad);
                using (var br = new SolidBrush(Color.White))
                using (var p = Theme.Rounded(left, size * 0.03f)) g.FillPath(br, p);
                float rx = scr.X + scr.Width / 2 + pad * 0.25f, rw = scr.Width / 2 - pad * 1.25f, rh = (scr.Height - 2 * pad - pad * 0.5f) / 2;
                using (var br = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
                {
                    using (var p = Theme.Rounded(new RectangleF(rx, scr.Y + pad, rw, rh), size * 0.025f)) g.FillPath(br, p);
                    using (var p = Theme.Rounded(new RectangleF(rx, scr.Y + pad + rh + pad * 0.5f, rw, rh), size * 0.025f)) g.FillPath(br, p);
                }
            }
            return b;
        }

        public static Icon AppIcon(int size)
        {
            using (var b = Logo(size))
            {
                IntPtr hi = b.GetHicon();
                var ic = (Icon)Icon.FromHandle(hi).Clone();
                Native.DestroyIcon(hi);
                return ic;
            }
        }

        // Escribe un .ico con varias resoluciones (PNG dentro) para el ejecutable
        public static void WriteIco(string path)
        {
            int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
            var pngs = new List<byte[]>();
            foreach (int s in sizes)
                using (var b = Logo(s))
                using (var ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
            using (var fs = File.Create(path))
            using (var w = new BinaryWriter(fs))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)0); w.Write((byte)0);
                    w.Write((short)1); w.Write((short)32);
                    w.Write(pngs[i].Length); w.Write(offset);
                    offset += pngs[i].Length;
                }
                foreach (var p in pngs) w.Write(p);
            }
        }
    }

    // Menú con aspecto de Windows 11 (claro/oscuro, esquinas redondeadas)
    public sealed class MenuRenderer : ToolStripRenderer
    {
        public Palette P;
        public MenuRenderer(Palette p) { P = p; }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(P.MenuBack);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            var r = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using (var pen = new Pen(P.Light ? Color.FromArgb(214, 214, 214) : Color.FromArgb(70, 70, 70))) e.Graphics.DrawRectangle(pen, r);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(Theme.S(4), Theme.S(1), e.Item.Width - Theme.S(8), e.Item.Height - Theme.S(2));
            using (var br = new SolidBrush(P.MenuHover))
            using (var p = Theme.Rounded(r, Theme.S(4))) g.FillPath(br, p);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var mi = e.Item as ToolStripMenuItem;
            bool shortcut = mi != null && !string.IsNullOrEmpty(mi.ShortcutKeyDisplayString) && e.Text == mi.ShortcutKeyDisplayString;
            e.TextColor = !e.Item.Enabled ? P.Disabled : (shortcut ? P.SubText : P.Text);
            if (shortcut)
            {
                var r = e.TextRectangle;
                r.X -= Theme.S(6);
                e.TextRectangle = r;
            }
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (var pen = new Pen(P.Sep)) e.Graphics.DrawLine(pen, Theme.S(10), y, e.Item.Width - Theme.S(10), y);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = e.ArrowRectangle;
            float cx = r.X + r.Width / 2f - Theme.S(2), cy = r.Y + r.Height / 2f, h = Theme.S(3.5f);
            using (var pen = new Pen(e.Item.Enabled ? P.SubText : P.Disabled, Math.Max(1f, Theme.S(1.2f))))
            {
                g.DrawLine(pen, cx - h / 2, cy - h, cx + h / 2, cy);
                g.DrawLine(pen, cx + h / 2, cy, cx - h / 2, cy + h);
            }
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = e.ImageRectangle;
            using (var pen = new Pen(P.Text, Math.Max(1.2f, Theme.S(1.5f))))
            {
                g.DrawLine(pen, r.X + r.Width * 0.18f, r.Y + r.Height * 0.52f, r.X + r.Width * 0.42f, r.Y + r.Height * 0.76f);
                g.DrawLine(pen, r.X + r.Width * 0.42f, r.Y + r.Height * 0.76f, r.X + r.Width * 0.84f, r.Y + r.Height * 0.26f);
            }
        }
    }
}
