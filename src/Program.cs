using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Encuadre
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            try { if (!Native.SetProcessDpiAwarenessContext(new IntPtr(-4))) Native.SetProcessDPIAware(); }
            catch { try { Native.SetProcessDPIAware(); } catch { } }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length >= 2 && args[0] == "--write-icon") { Icons.WriteIco(args[1]); return 0; }
            if (args.Length >= 2 && args[0] == "--selftest") return SelfTest.Run(args[1]);
            if (args.Length >= 3 && args[0] == "--shot") { if (args.Length >= 4) Theme.Force = args[3]; return Shots.Run(args[1], args[2]); }

            bool created;
            using (var mutex = new Mutex(true, @"Local\Encuadre.SingleInstance", out created))
            using (var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Encuadre.ShowPrefs"))
            {
                if (!created)
                {
                    showEvent.Set(); // ya está abierto: mostrar sus preferencias
                    return 0;
                }
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Log.Write("UI", e.Exception); };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Log.Write("Fatal: " + e.ExceptionObject); };
                var app = new App(Config.Load(), showEvent, false);
                Application.Run(app);
                GC.KeepAlive(mutex);
            }
            return 0;
        }
    }

    // Capturas de la interfaz para revisar el diseño
    static class Shots
    {
        public static int Run(string what, string png)
        {
            if (what.StartsWith("trayprev")) return TrayPreview(png, what.Length > 9 ? what.Substring(9) : null);
            var cfg = Config.Defaults();
            cfg.InMemory = true;
            var app = new App(cfg, null, true);
            if (what == "menu")
            {
                var menu = app.BuildMenu();
                var wa = Screen.PrimaryScreen.WorkingArea;
                menu.Show(new Point(wa.Right - Theme.S(420), wa.Top + Theme.S(40)));
                Pump(400);
                var b = menu.Bounds;
                Capture(new Rectangle(b.X - 1, b.Y - 1, b.Width + 2, b.Height + 2), png);
                menu.Close();
                return 0;
            }
            int tab = what.EndsWith("1") ? 1 : what.EndsWith("2") ? 2 : 0;
            var f = new PrefsForm(app, tab);
            // fuera de la pantalla y sin activar: se captura solo esta ventana, sin tocar lo que haya en el escritorio
            f.Location = new Point(-20000, -20000);
            f.Show();
            Pump(700);
            using (var b = new Bitmap(f.Width, f.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(b))
                {
                    IntPtr dc = g.GetHdc();
                    Native.PrintWindow(f.Handle, dc, 2);
                    g.ReleaseHdc(dc);
                }
                b.Save(png, ImageFormat.Png);
            }
            f.Close();
            return 0;
        }

        // Vista previa de las variantes del icono de bandeja, a tamaño real y ampliadas,
        // colocadas sobre una captura de la bandeja del usuario si se da (trayprev:<ruta>)
        static int TrayPreview(string png, string trayShot)
        {
            int variants = 3, zoom = 8;
            Bitmap shot = null;
            if (!string.IsNullOrEmpty(trayShot) && File.Exists(trayShot)) shot = new Bitmap(trayShot);
            int rowH = Math.Max(16 * zoom + 20, shot == null ? 0 : shot.Height + 20);
            int wTotal = 20 + (shot == null ? 0 : shot.Width + 30) + 16 * zoom * 2 + 60;
            using (var outB = new Bitmap(wTotal, rowH * variants + 10, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(outB))
            {
                g.Clear(Color.FromArgb(20, 20, 20));
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                // localizar el icono viejo (primer icono arriba a la izquierda de la captura)
                Rectangle old = Rectangle.Empty;
                if (shot != null)
                {
                    int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
                    for (int y = 10; y < Math.Min(shot.Height, 55); y++)
                        for (int x = 10; x < Math.Min(shot.Width, 58); x++)
                        {
                            var c = shot.GetPixel(x, y);
                            if (c.R > 150 && c.G > 150 && c.B > 150) { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
                        }
                    if (maxX >= 0) old = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
                }
                for (int v = 0; v < variants; v++)
                {
                    int y0 = 10 + v * rowH, x = 20;
                    using (var ic = Icons.TrayBitmap(16, false, v))
                    {
                        if (shot != null)
                        {
                            using (var copy = new Bitmap(shot))
                            {
                                if (!old.IsEmpty)
                                {
                                    using (var cg = Graphics.FromImage(copy))
                                    {
                                        var bg = shot.GetPixel(old.Left - 3, old.Top + old.Height / 2);
                                        using (var br = new SolidBrush(bg)) cg.FillRectangle(br, Rectangle.Inflate(old, 1, 1));
                                        int cx = old.Left + old.Width / 2 - 8, cy = old.Top + old.Height / 2 - 8;
                                        cg.DrawImageUnscaled(ic, cx, cy);
                                    }
                                }
                                g.DrawImageUnscaled(copy, x, y0);
                            }
                            x += shot.Width + 30;
                        }
                        using (var bgb = new SolidBrush(Color.FromArgb(44, 44, 44))) g.FillRectangle(bgb, x, y0, 16 * zoom, 16 * zoom);
                        g.DrawImage(ic, new Rectangle(x, y0, 16 * zoom, 16 * zoom));
                        x += 16 * zoom + 20;
                        using (var ic24 = Icons.TrayBitmap(24, false, v))
                        {
                            using (var bgb = new SolidBrush(Color.FromArgb(44, 44, 44))) g.FillRectangle(bgb, x, y0, 24 * 4, 24 * 4);
                            g.DrawImage(ic24, new Rectangle(x, y0, 24 * 4, 24 * 4));
                        }
                    }
                }
                outB.Save(png, ImageFormat.Png);
            }
            if (shot != null) shot.Dispose();
            return 0;
        }

        static void Pump(int ms)
        {
            var end = DateTime.Now.AddMilliseconds(ms);
            while (DateTime.Now < end) { Application.DoEvents(); Thread.Sleep(15); }
        }

        static void Capture(Rectangle r, string png)
        {
            using (var b = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(b)) g.CopyFromScreen(r.Location, Point.Empty, r.Size);
                b.Save(png, ImageFormat.Png);
            }
        }
    }

    // Pruebas automáticas sobre una ventana propia (no toca ventanas del usuario)
    static class SelfTest
    {
        static StringBuilder log = new StringBuilder();
        static int fails;

        static void Check(string name, bool ok, string detail)
        {
            if (!ok) fails++;
            log.AppendLine((ok ? "OK    " : "FALLO ") + name + (detail != null ? "  " + detail : ""));
        }

        static void Pump(int ms)
        {
            var end = DateTime.Now.AddMilliseconds(ms);
            while (DateTime.Now < end) { Application.DoEvents(); Thread.Sleep(10); }
        }

        static void Key(ushort vk, bool up, bool ext)
        {
            var i = new INPUT[1];
            i[0].type = Native.INPUT_KEYBOARD;
            i[0].U.ki.wVk = vk;
            i[0].U.ki.dwFlags = (up ? Native.KEYEVENTF_KEYUP : 0) | (ext ? Native.KEYEVENTF_EXTENDEDKEY : 0);
            Native.SendInput(1, i, Marshal.SizeOf(typeof(INPUT)));
        }

        public static int Run(string outPath)
        {
            Win.AllowOwnProcess = true;
            var cfg = Config.Defaults();
            cfg.InMemory = true;
            var exec = new Executor(delegate { return cfg; });
            var f = new Form { Text = "Encuadre - prueba", StartPosition = FormStartPosition.Manual, FormBorderStyle = FormBorderStyle.Sizable };
            var mon0 = Monitors.FromPoint(Cursor.Position.X, Cursor.Position.Y);
            f.Bounds = new Rectangle(mon0.Work.Left + 100, mon0.Work.Top + 100, 700, 500);
            f.Show();
            Pump(300);
            IntPtr h = f.Handle;
            var mon = Monitors.FromWindow(h);
            RECT w = mon.Work;
            log.AppendLine("Monitor trabajo: " + w + "  monitores: " + Monitors.All().Count + "  DPI ventana: " + Win.Dpi(h));
            log.AppendLine("Gestionable (propia): " + Win.IsManageable(h));

            Check("Atajo por defecto: Win+Alt+← = mitad izquierda", cfg.ShortcutFor(ActionId.LeftHalf).ToString() == "Win+Alt+←", cfg.ShortcutFor(ActionId.LeftHalf).ToString());
            // Comparación con el código de Rectangle (alternateDefault), con ⌃⌥ → Win+Alt
            string[][] rect = {
                new[] { "LeftHalf", "Win+Alt+←" }, new[] { "RightHalf", "Win+Alt+→" }, new[] { "TopHalf", "Win+Alt+↑" }, new[] { "BottomHalf", "Win+Alt+↓" },
                new[] { "TopLeft", "Win+Alt+U" }, new[] { "TopRight", "Win+Alt+I" }, new[] { "BottomLeft", "Win+Alt+J" }, new[] { "BottomRight", "Win+Alt+K" },
                new[] { "FirstThird", "Win+Alt+D" }, new[] { "CenterThird", "Win+Alt+F" }, new[] { "LastThird", "Win+Alt+G" },
                new[] { "FirstTwoThirds", "Win+Alt+E" }, new[] { "CenterTwoThirds", "Win+Alt+R" }, new[] { "LastTwoThirds", "Win+Alt+T" },
                new[] { "Maximize", "Win+Alt+Intro" }, new[] { "MaximizeHeight", "Win+Alt+Mayús+↑" }, new[] { "Center", "Win+Alt+C" },
                new[] { "Restore", "Win+Alt+Retroceso" }, new[] { "NextDisplay", "Win+Ctrl+Alt+→" }, new[] { "PreviousDisplay", "Win+Ctrl+Alt+←" },
            };
            foreach (var r in rect)
            {
                var id = (ActionId)Enum.Parse(typeof(ActionId), r[0]);
                Check("Atajo " + r[0] + " = " + r[1], cfg.ShortcutFor(id).ToString() == r[1], cfg.ShortcutFor(id).ToString());
            }
            ActionId[] tiles = {
                ActionId.LeftHalf, ActionId.RightHalf, ActionId.CenterHalf, ActionId.TopHalf, ActionId.BottomHalf,
                ActionId.TopLeft, ActionId.TopRight, ActionId.BottomLeft, ActionId.BottomRight,
                ActionId.FirstThird, ActionId.CenterThird, ActionId.LastThird, ActionId.FirstTwoThirds, ActionId.CenterTwoThirds, ActionId.LastTwoThirds,
                ActionId.Maximize, ActionId.FirstFourth, ActionId.LastThreeFourths, ActionId.TopLeftSixth, ActionId.BottomRightSixth
            };
            foreach (var a in tiles)
            {
                exec.Forget(h);
                exec.Run(a, h, false, default(RECT));
                Pump(60);
                RECT got = Native.GetFrame(h);
                RECT exp = Executor.TileFor(a, 0, mon, cfg, 0);
                Check(a.ToString(), got.Near(exp, 1), "esperado " + exp + " obtenido " + got);
            }

            // ciclo ½ → ⅔ → ⅓
            exec.Forget(h);
            int[] expectW = { (int)Math.Round(w.Width / 2.0), (int)Math.Round(w.Width * 2 / 3.0), (int)Math.Round(w.Width / 3.0), (int)Math.Round(w.Width / 2.0) };
            for (int i = 0; i < 4; i++)
            {
                exec.Run(ActionId.LeftHalf, h, false, default(RECT));
                Pump(60);
                RECT got = Native.GetFrame(h);
                Check("Mitad izquierda repetida #" + (i + 1), Math.Abs(got.Width - expectW[i]) <= 1 && got.Left == w.Left, "ancho " + got.Width + " esperado " + expectW[i]);
            }

            // tercios que rotan
            exec.Forget(h);
            ActionId[] rot = { ActionId.FirstThird, ActionId.CenterThird, ActionId.LastThird };
            for (int i = 0; i < 3; i++)
            {
                exec.Run(ActionId.FirstThird, h, false, default(RECT));
                Pump(60);
                Check("Primer tercio repetido #" + (i + 1), Native.GetFrame(h).Near(Executor.TileFor(rot[i], 0, mon, cfg, 0), 1), null);
            }

            // cuartos que rotan (como Rectangle)
            exec.Forget(h);
            ActionId[] rot4 = { ActionId.FirstFourth, ActionId.SecondFourth, ActionId.ThirdFourth, ActionId.LastFourth };
            for (int i = 0; i < 4; i++)
            {
                exec.Run(ActionId.FirstFourth, h, false, default(RECT));
                Pump(60);
                Check("Primer cuarto repetido #" + (i + 1), Native.GetFrame(h).Near(Executor.TileFor(rot4[i], 0, mon, cfg, 0), 1), null);
            }

            // restaurar
            exec.Forget(h);
            Win.SetFrame(h, RECT.XYWH(w.Left + 150, w.Top + 120, 640, 420));
            Pump(60);
            RECT before = Native.GetFrame(h);
            exec.Run(ActionId.RightHalf, h, false, default(RECT));
            exec.Run(ActionId.TopHalf, h, false, default(RECT));
            exec.Run(ActionId.Restore, h, false, default(RECT));
            Pump(60);
            Check("Restaurar", Native.GetFrame(h).Near(before, 1), "antes " + before + " después " + Native.GetFrame(h));

            // centrar, más grande/pequeña, maximizar altura, casi maximizar
            exec.Run(ActionId.Center, h, false, default(RECT));
            Pump(40);
            RECT c = Native.GetFrame(h);
            Check("Centrar", Math.Abs((c.Left + c.Right) / 2 - (w.Left + w.Right) / 2) <= 1 && Math.Abs((c.Top + c.Bottom) / 2 - (w.Top + w.Bottom) / 2) <= 1 && c.Width == before.Width, c.ToString());
            exec.Run(ActionId.Larger, h, false, default(RECT));
            Pump(40);
            RECT lg = Native.GetFrame(h);
            Check("Más grande", lg.Width > c.Width && lg.Height > c.Height, c + " -> " + lg);
            exec.Run(ActionId.Smaller, h, false, default(RECT));
            exec.Run(ActionId.Smaller, h, false, default(RECT));
            Pump(40);
            RECT sm = Native.GetFrame(h);
            Check("Más pequeña", sm.Width < lg.Width && sm.Height < lg.Height, lg + " -> " + sm);
            exec.Run(ActionId.MaximizeHeight, h, false, default(RECT));
            Pump(40);
            RECT mh = Native.GetFrame(h);
            Check("Maximizar altura", mh.Top == w.Top && mh.Bottom == w.Bottom && mh.Width == sm.Width, mh.ToString());
            exec.Run(ActionId.AlmostMaximize, h, false, default(RECT));
            Pump(40);
            Check("Casi maximizar", Native.GetFrame(h).Near(Layout.AlmostMaximize(w), 1), Native.GetFrame(h).ToString());

            // ventana maximizada de verdad → mitad izquierda → restaurar vuelve a maximizar
            exec.Forget(h);
            f.WindowState = FormWindowState.Maximized;
            Pump(150);
            exec.Run(ActionId.LeftHalf, h, false, default(RECT));
            Pump(100);
            Check("Desde maximizada a mitad", !Native.IsZoomed(h) && Native.GetFrame(h).Near(Executor.TileFor(ActionId.LeftHalf, 0, mon, cfg, 0), 1), Native.GetFrame(h).ToString());
            exec.Run(ActionId.Restore, h, false, default(RECT));
            Pump(150);
            Check("Restaurar a maximizada", Native.IsZoomed(h), null);
            f.WindowState = FormWindowState.Normal;
            Pump(150);

            // huecos
            cfg.Gap = 10;
            exec.Forget(h);
            exec.Run(ActionId.LeftHalf, h, false, default(RECT));
            Pump(60);
            RECT gp = Native.GetFrame(h);
            int mid = w.Left + (int)Math.Round(w.Width / 2.0);
            Check("Huecos 10 px", gp.Left == w.Left + 10 && gp.Top == w.Top + 10 && gp.Right == mid - 5 && gp.Bottom == w.Bottom - 10, gp.ToString());
            cfg.Gap = 0;

            // áreas de ajuste
            RECT b = mon.Bounds;
            Check("Área borde izq.", Area(cfg, b.Left, (b.Top + b.Bottom) / 2) == ActionId.LeftHalf, null);
            Check("Área borde der.", Area(cfg, b.Right - 1, (b.Top + b.Bottom) / 2) == ActionId.RightHalf, null);
            Check("Área arriba", Area(cfg, (b.Left + b.Right) / 2, b.Top) == ActionId.Maximize, null);
            Check("Área esquina sup. izq.", Area(cfg, b.Left, b.Top + 5) == ActionId.TopLeft, null);
            Check("Área esquina inf. der.", Area(cfg, b.Right - 1, b.Bottom - 3) == ActionId.BottomRight, null);
            Check("Área izq. parte alta", Area(cfg, b.Left, b.Top + 100) == ActionId.TopHalf, null);
            Check("Área abajo tercio central", Area(cfg, (b.Left + b.Right) / 2, b.Bottom - 1) == ActionId.CenterThird, null);
            var r1 = SnapManager.Compute(cfg, b.Left + b.Width / 6, b.Bottom - 1, ActionId.CenterThird);
            Check("Área abajo: de central a primero = ⅔", r1 != null && r1.Action == ActionId.FirstTwoThirds, null);
            Check("Centro de pantalla sin área", SnapManager.Compute(cfg, (b.Left + b.Right) / 2, (b.Top + b.Bottom) / 2, ActionId.None) == null, null);

            // atajos de teclado reales a través del gancho
            var invoker = new Control();
            invoker.CreateControl();
            var hk = invoker.Handle;
            var hooks = new InputHooks(invoker);
            // Se usa F24 (una tecla que ninguna app usa) para no molestar a la app que tenga el foco.
            var map = new System.Collections.Generic.Dictionary<long, ActionId>();
            map[new Shortcut(Mod.Ctrl | Mod.Alt, 0x87).Key] = ActionId.RightHalf;
            map[new Shortcut(Mod.Ctrl | Mod.Alt | Mod.Shift, 0x87).Key] = ActionId.MaximizeHeight;
            map[new Shortcut(Mod.Win | Mod.Alt, 0x87).Key] = ActionId.LeftHalf;
            hooks.Map = map;
            var firedList = new System.Collections.Generic.List<ActionId>();
            hooks.OnHotkey = delegate(ActionId a, IntPtr hw) { firedList.Add(a); };
            hooks.MouseEnabled = false;
            hooks.Start();
            Pump(100);
            // Ctrl izq. + Alt izq. + F24
            Key(0xA2, false, false); Key(0xA4, false, false); Key(0x87, false, false);
            Key(0x87, true, false); Key(0xA4, true, false); Key(0xA2, true, false);
            Pump(250);
            Check("Gancho: Ctrl+Alt+tecla dispara la acción", firedList.Count == 1 && firedList[0] == ActionId.RightHalf, "disparos " + firedList.Count);
            // AltGr (Ctrl izq. falso + Alt der.) + F24: NO debe dispararse
            int before2 = firedList.Count;
            Key(0xA2, false, false); Key(0xA5, false, true); Key(0x87, false, false);
            Key(0x87, true, false); Key(0xA5, true, true); Key(0xA2, true, false);
            Pump(250);
            Check("Gancho: AltGr no cuenta como Ctrl+Alt (€, @ siguen funcionando)", firedList.Count == before2, null);
            // Win izq. + Alt izq. + F24 (los atajos nuevos por defecto)
            Key(0x5B, false, true); Key(0xA4, false, false); Key(0x87, false, false);
            Key(0x87, true, false); Key(0xA4, true, false); Key(0x5B, true, true);
            Pump(250);
            Check("Gancho: Win+Alt dispara la acción", firedList.Count == before2 + 1 && firedList[firedList.Count - 1] == ActionId.LeftHalf, null);
            // Win + AltGr + F24 también vale como Win+Alt
            Key(0x5B, false, true); Key(0xA2, false, false); Key(0xA5, false, true); Key(0x87, false, false);
            Key(0x87, true, false); Key(0xA5, true, true); Key(0xA2, true, false); Key(0x5B, true, true);
            Pump(250);
            Check("Gancho: Win+AltGr funciona como Win+Alt", firedList.Count == before2 + 2 && firedList[firedList.Count - 1] == ActionId.LeftHalf, null);
            before2 = firedList.Count;
            // Ctrl+Alt+Mayús+F24
            Key(0xA2, false, false); Key(0xA4, false, false); Key(0xA0, false, false); Key(0x87, false, false);
            Key(0x87, true, false); Key(0xA0, true, false); Key(0xA4, true, false); Key(0xA2, true, false);
            Pump(250);
            Check("Gancho: Ctrl+Alt+Mayús", firedList.Count == before2 + 1 && firedList[firedList.Count - 1] == ActionId.MaximizeHeight, null);
            // sin modificadores no hace nada
            int before3 = firedList.Count;
            Key(0x87, false, false); Key(0x87, true, false);
            Pump(200);
            Check("Gancho: tecla sola no dispara", firedList.Count == before3, null);
            hooks.Dispose();
            f.Close();

            log.AppendLine(fails == 0 ? "TODO OK" : fails + " FALLOS");
            File.WriteAllText(outPath, log.ToString(), new UTF8Encoding(true));
            return fails;
        }

        static ActionId Area(Config cfg, int x, int y)
        {
            var r = SnapManager.Compute(cfg, x, y, ActionId.None);
            return r == null ? ActionId.None : r.Action;
        }
    }
}
