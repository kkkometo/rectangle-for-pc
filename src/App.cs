using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Encuadre
{
    public sealed class App : ApplicationContext
    {
        public Config Config;
        public Executor Exec;
        public InputHooks Hooks;
        public SnapManager Snap;
        readonly Control invoker;
        NotifyIcon tray;
        ContextMenuStrip menu;
        Palette menuPalette;
        readonly Dictionary<ActionId, ToolStripMenuItem> items = new Dictionary<ActionId, ToolStripMenuItem>();
        ToolStripMenuItem ignoreItem;
        IntPtr lastTarget;
        WinEventProc fgProc, endProc;
        IntPtr fgHook, endHook;
        PrefsForm prefs;
        RegisteredWaitHandle showWait;
        readonly bool headless;

        public App(Config cfg, EventWaitHandle showEvent, bool headless)
        {
            Config = cfg;
            this.headless = headless;
            invoker = new Control();
            invoker.CreateControl();
            var h = invoker.Handle;
            Exec = new Executor(delegate { return Config; });
            if (headless) return;

            Hooks = new InputHooks(invoker);
            Hooks.IsIgnored = IsIgnored;
            Hooks.OnHotkey = HotkeyPressed;
            Snap = new SnapManager(this);
            Hooks.OnLeftDown = Snap.LeftDown;
            Hooks.OnLeftUp = Snap.LeftUp;
            Hooks.MouseEnabled = Config.SnapEnabled;
            RebuildMap();
            Hooks.Start();

            fgProc = OnForeground;
            endProc = OnMoveSizeEnd;
            fgHook = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, fgProc, 0, 0, Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS);
            endHook = Native.SetWinEventHook(Native.EVENT_SYSTEM_MOVESIZEEND, Native.EVENT_SYSTEM_MOVESIZEEND, IntPtr.Zero, endProc, 0, 0, Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS);
            IntPtr fg = Win.Root(Native.GetForegroundWindow());
            if (Win.IsManageable(fg)) lastTarget = fg;

            tray = new NotifyIcon();
            tray.Text = "Encuadre";
            tray.MouseUp += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowTrayMenu(); };
            RefreshTheme();
            tray.Visible = !Config.HideTrayIcon;
            SystemEvents.UserPreferenceChanged += OnUserPrefChanged;

            if (showEvent != null)
                showWait = ThreadPool.RegisterWaitForSingleObject(showEvent, delegate { invoker.BeginInvoke(new Action(delegate { ShowPrefs(-1); })); }, null, -1, false);

            if (Config.FirstRun)
            {
                Config.FirstRun = false;
                Config.Save();
                if (tray.Visible)
                    tray.ShowBalloonTip(10000, "Encuadre está funcionando",
                        "Win+Alt+← / → mitades · Win+Alt+Intro maximizar · o arrastra una ventana a un borde. Haz clic en el icono para ver todas las acciones.",
                        ToolTipIcon.None);
            }
        }

        public bool IsIgnored(IntPtr h)
        {
            if (Config.Ignored.Count == 0) return false;
            return Config.IsIgnored(Win.ExeName(h));
        }

        void HotkeyPressed(ActionId a, IntPtr h)
        {
            try
            {
                if (!Win.IsManageable(h)) return;
                Exec.Run(a, h, false, default(RECT));
            }
            catch (Exception ex) { Log.Write("Hotkey " + a, ex); }
        }

        void OnForeground(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            if (idObject != 0) return;
            IntPtr r = Win.Root(hwnd);
            if (Win.IsManageable(r)) lastTarget = r;
        }

        void OnMoveSizeEnd(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            if (idObject != 0 || Snap == null) return;
            Snap.MoveSizeEnded(Win.Root(hwnd));
        }

        void OnUserPrefChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.VisualStyle || e.Category == UserPreferenceCategory.Color)
                invoker.BeginInvoke(new Action(RefreshTheme));
        }

        public void RebuildMap()
        {
            var map = new Dictionary<long, ActionId>();
            foreach (var kv in Config.Shortcuts) if (!kv.Value.IsEmpty) map[kv.Value.Key] = kv.Key;
            if (Hooks != null) Hooks.Map = map;
        }

        public void ShortcutsChanged()
        {
            RebuildMap();
            foreach (var kv in items) kv.Value.ShortcutKeyDisplayString = Config.ShortcutFor(kv.Key).ToString();
        }

        public void SnapSettingsChanged() { if (Hooks != null) Hooks.MouseEnabled = Config.SnapEnabled; }

        public void TrayVisibilityChanged() { if (tray != null) tray.Visible = !Config.HideTrayIcon; }

        void RefreshTheme()
        {
            if (tray == null) return;
            var old = tray.Icon;
            tray.Icon = Icons.Tray(SystemInformation.SmallIconSize.Width, Theme.SystemLight);
            if (old != null) old.Dispose();
            var oldMenu = menu;
            BuildMenu();
            tray.ContextMenuStrip = menu;
            if (oldMenu != null) oldMenu.Dispose();
        }

        // ---------------- Menú ----------------
        public ContextMenuStrip BuildMenu()
        {
            menuPalette = Palette.Create(Theme.SystemLight);
            items.Clear();
            menu = NewDropDown(new ContextMenuStrip());
            var groups = new[] { 0, 1, 2, 3, 4 };
            bool first = true;
            foreach (int g in groups)
            {
                if (!first) menu.Items.Add(NewSeparator());
                first = false;
                foreach (var d in Actions.All) if (d.Group == g) menu.Items.Add(NewActionItem(d));
            }
            menu.Items.Add(NewSeparator());
            menu.Items.Add(NewSubmenu("Cuartos", ActionId.FirstFourth, 6));
            menu.Items.Add(NewSubmenu("Sextos", ActionId.TopLeftSixth, 7));
            menu.Items.Add(NewSubmenu("Mover", ActionId.MoveLeft, 5));
            menu.Items.Add(NewSeparator());
            ignoreItem = NewItem("Ignorar app", delegate { ToggleIgnore(); });
            menu.Items.Add(ignoreItem);
            menu.Items.Add(NewItem("Preferencias…", delegate { ShowPrefs(0); }));
            menu.Items.Add(NewSeparator());
            menu.Items.Add(NewItem("Salir de Encuadre", delegate { Quit(); }));
            menu.Opening += delegate { UpdateMenuState(); };
            return menu;
        }

        T NewDropDown<T>(T dd) where T : ToolStripDropDownMenu
        {
            dd.Renderer = new MenuRenderer(menuPalette);
            dd.Font = Theme.UiFont(9f);
            dd.ImageScalingSize = new Size(Theme.S(16), Theme.S(16));
            dd.ShowImageMargin = true;
            dd.ShowCheckMargin = false;
            dd.Padding = new Padding(Theme.S(2), Theme.S(4), Theme.S(2), Theme.S(4));
            dd.DropShadowEnabled = false;
            dd.HandleCreated += delegate { Native.SetRoundCorners(dd.Handle, 2); };
            return dd;
        }

        ToolStripSeparator NewSeparator()
        {
            return new ToolStripSeparator { AutoSize = false, Height = Theme.S(9) };
        }

        ToolStripMenuItem NewItem(string text, EventHandler click)
        {
            var it = new ToolStripMenuItem(text, null, click);
            it.Padding = new Padding(Theme.S(4), Theme.S(5), Theme.S(4), Theme.S(5));
            return it;
        }

        ToolStripMenuItem NewActionItem(ActionDef d)
        {
            ActionId id = d.Id;
            var it = NewItem(d.Name, delegate { RunFromMenu(id); });
            it.Image = Icons.Action(id, Theme.S(16), menuPalette.Text);
            it.ImageScaling = ToolStripItemImageScaling.None;
            it.ShortcutKeyDisplayString = Config.ShortcutFor(id).ToString();
            it.ShowShortcutKeys = true;
            items[id] = it;
            return it;
        }

        ToolStripMenuItem NewSubmenu(string text, ActionId icon, int group)
        {
            var it = NewItem(text, null);
            it.Image = Icons.Action(icon, Theme.S(16), menuPalette.Text);
            it.ImageScaling = ToolStripItemImageScaling.None;
            NewDropDown((ToolStripDropDownMenu)it.DropDown);
            foreach (var d in Actions.All) if (d.Group == group) it.DropDownItems.Add(NewActionItem(d));
            return it;
        }

        bool TargetOk { get { return lastTarget != IntPtr.Zero && Win.IsManageable(lastTarget); } }

        void UpdateMenuState()
        {
            bool ok = TargetOk;
            foreach (var it in items.Values) it.Enabled = ok;
            ignoreItem.Visible = ok;
            if (ok)
            {
                string name = Win.AppDisplayName(lastTarget) ?? "esta app";
                if (name.Length > 32) name = name.Substring(0, 30) + "…";
                ignoreItem.Text = (IsIgnored(lastTarget) ? "Dejar de ignorar «" : "Ignorar «") + name + "»";
            }
        }

        void RunFromMenu(ActionId a)
        {
            if (!TargetOk) return;
            try
            {
                Exec.Run(a, lastTarget, false, default(RECT));
                Native.SetForegroundWindow(lastTarget);
            }
            catch (Exception ex) { Log.Write("Menu " + a, ex); }
        }

        void ToggleIgnore()
        {
            if (!TargetOk) return;
            string exe = Win.ExeName(lastTarget);
            if (string.IsNullOrEmpty(exe)) return;
            lock (Config.Ignored)
            {
                if (Config.IsIgnored(exe)) Config.Ignored.RemoveAll(delegate(string s) { return string.Equals(s, exe, StringComparison.OrdinalIgnoreCase); });
                else Config.Ignored.Add(exe);
            }
            Config.Save();
        }

        void ShowTrayMenu()
        {
            try
            {
                var mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
                if (mi != null) mi.Invoke(tray, null);
            }
            catch (Exception ex) { Log.Write("ShowTrayMenu", ex); }
        }

        public void ShowPrefs(int tab)
        {
            if (prefs == null || prefs.IsDisposed)
            {
                prefs = new PrefsForm(this, tab < 0 ? 0 : tab);
                prefs.Show();
            }
            else if (tab >= 0) prefs.SelectTab(tab);
            if (prefs.WindowState == FormWindowState.Minimized) prefs.WindowState = FormWindowState.Normal;
            prefs.Activate();
            Native.SetForegroundWindow(prefs.Handle);
        }

        // ---------------- Inicio con Windows ----------------
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public bool LaunchAtLogin
        {
            get
            {
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                        return k != null && k.GetValue("Encuadre") != null;
                }
                catch { return false; }
            }
            set
            {
                try
                {
                    using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                    {
                        if (value) k.SetValue("Encuadre", "\"" + Application.ExecutablePath + "\"");
                        else k.DeleteValue("Encuadre", false);
                    }
                }
                catch (Exception ex) { Log.Write("LaunchAtLogin", ex); }
            }
        }

        public void Quit()
        {
            try
            {
                if (prefs != null && !prefs.IsDisposed) prefs.Close();
                SystemEvents.UserPreferenceChanged -= OnUserPrefChanged;
                if (Hooks != null) Hooks.Dispose();
                if (fgHook != IntPtr.Zero) Native.UnhookWinEvent(fgHook);
                if (endHook != IntPtr.Zero) Native.UnhookWinEvent(endHook);
                if (showWait != null) showWait.Unregister(null);
                if (Snap != null) Snap.Dispose();
                if (tray != null) { tray.Visible = false; tray.Dispose(); }
            }
            catch (Exception ex) { Log.Write("Quit", ex); }
            ExitThread();
        }
    }
}
