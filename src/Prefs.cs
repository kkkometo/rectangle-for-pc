using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Encuadre
{
    static class UI
    {
        public static int S(float v) { return Theme.S(v); }

        public static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        public static void FillRound(Graphics g, Color c, RectangleF r, float rad)
        {
            using (var b = new SolidBrush(c))
            using (var p = Theme.Rounded(r, rad)) g.FillPath(b, p);
        }

        public static void StrokeRound(Graphics g, Color c, RectangleF r, float rad, float width)
        {
            using (var pen = new Pen(c, width))
            using (var p = Theme.Rounded(r, rad)) g.DrawPath(pen, p);
        }

        public static void Text(Graphics g, string s, Font f, Color c, Rectangle r, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, s, f, r, c, flags | TextFormatFlags.NoPrefix);
        }
    }

    abstract class FlatControl : Control
    {
        protected Palette P;
        protected bool hover;
        protected FlatControl(Palette p)
        {
            P = p;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    }

    sealed class FlatCheck : FlatControl
    {
        bool isChecked;
        public event EventHandler CheckedChanged;
        public FlatCheck(Palette p, string text, bool value) : base(p)
        {
            Text = text;
            isChecked = value;
            Cursor = Cursors.Hand;
            Height = UI.S(28);
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
        }
        public bool Checked
        {
            get { return isChecked; }
            set { if (isChecked == value) return; isChecked = value; Invalidate(); if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); }
        }
        protected override void OnClick(EventArgs e) { Checked = !Checked; Focus(); base.OnClick(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) Checked = !Checked; base.OnKeyDown(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            float box = UI.S(18);
            var r = new RectangleF(UI.S(1), (Height - box) / 2f, box, box);
            if (isChecked)
            {
                UI.FillRound(g, hover ? ControlPaint.Light(P.Accent, 0.1f) : P.Accent, r, UI.S(4));
                using (var pen = new Pen(P.AccentText, Math.Max(1.4f, UI.S(1.6f))) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(pen, r.X + box * 0.24f, r.Y + box * 0.52f, r.X + box * 0.43f, r.Y + box * 0.70f);
                    g.DrawLine(pen, r.X + box * 0.43f, r.Y + box * 0.70f, r.X + box * 0.77f, r.Y + box * 0.32f);
                }
            }
            else
            {
                UI.FillRound(g, hover ? P.SurfaceHover : P.Field, r, UI.S(4));
                UI.StrokeRound(g, P.SubText, r, UI.S(4), Math.Max(1f, UI.S(1f)));
            }
            int tx = (int)(r.Right + UI.S(10));
            UI.Text(g, Text, Font, P.Text, new Rectangle(tx, 0, Width - tx, Height), TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues) UI.StrokeRound(g, P.Accent, new RectangleF(r.X - 2, r.Y - 2, r.Width + 4, r.Height + 4), UI.S(5), 1);
        }
    }

    sealed class FlatButton : FlatControl
    {
        public bool Primary { get; set; }
        public FlatButton(Palette p, string text) : base(p)
        {
            Text = text;
            Cursor = Cursors.Hand;
            Height = UI.S(32);
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
            using (var g = CreateGraphics())
                Width = TextRenderer.MeasureText(g, text, Theme.UiFont(9.75f)).Width + UI.S(32);
        }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) OnClick(EventArgs.Empty); base.OnKeyDown(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            if (Primary) UI.FillRound(g, hover ? ControlPaint.Light(P.Accent, 0.15f) : P.Accent, r, UI.S(5));
            else
            {
                UI.FillRound(g, hover ? P.SurfaceHover : P.Surface, r, UI.S(5));
                UI.StrokeRound(g, P.Border, r, UI.S(5), 1);
            }
            UI.Text(g, Text, Font, Primary ? P.AccentText : P.Text, ClientRectangle, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    sealed class Slider : FlatControl
    {
        int value, max;
        bool dragging;
        public event EventHandler ValueChanged;
        public Slider(Palette p, int max, int value) : base(p)
        {
            this.max = max; this.value = value;
            Height = UI.S(28);
            Cursor = Cursors.Hand;
        }
        public int Value
        {
            get { return value; }
            set
            {
                int v = Math.Max(0, Math.Min(max, value));
                if (v == this.value) return;
                this.value = v; Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }
        float Pad { get { return UI.S(10); } }
        void SetFrom(int x) { Value = (int)Math.Round((x - Pad) / (Width - 2 * Pad) * max); }
        protected override void OnMouseDown(MouseEventArgs e) { dragging = true; SetFrom(e.X); base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (dragging) SetFrom(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { dragging = false; base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            float cy = Height / 2f, th = UI.S(4);
            float x = Pad + (Width - 2 * Pad) * value / (float)max;
            UI.FillRound(g, P.FieldBorder, new RectangleF(Pad, cy - th / 2, Width - 2 * Pad, th), th / 2);
            UI.FillRound(g, P.Accent, new RectangleF(Pad, cy - th / 2, x - Pad, th), th / 2);
            float r = UI.S(9);
            using (var b = new SolidBrush(P.Light ? Color.White : Color.FromArgb(69, 69, 69))) g.FillEllipse(b, x - r, cy - r, 2 * r, 2 * r);
            using (var pen = new Pen(P.Border)) g.DrawEllipse(pen, x - r, cy - r, 2 * r, 2 * r);
            float ir = hover || dragging ? UI.S(6) : UI.S(5);
            using (var b = new SolidBrush(P.Accent)) g.FillEllipse(b, x - ir, cy - ir, 2 * ir, 2 * ir);
        }
    }

    // Campo para grabar un atajo (como los de Rectangle)
    sealed class ShortcutBox : FlatControl
    {
        public readonly ActionId Action;
        readonly PrefsForm owner;
        Shortcut value;
        bool recording;
        readonly Action<int, int> onKey;

        public ShortcutBox(Palette p, PrefsForm owner, ActionId a, Shortcut s) : base(p)
        {
            this.owner = owner;
            Action = a;
            value = s;
            onKey = OnKey;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
        }

        public Shortcut Value { get { return value; } set { this.value = value; Invalidate(); } }
        public bool Recording { get { return recording; } }
        Rectangle ClearRect { get { return new Rectangle(Width - Height, 0, Height, Height); } }

        public void StartRecording()
        {
            owner.StopRecordingAll();
            recording = true;
            Focus();
            if (owner.App.Hooks != null) owner.App.Hooks.Recorder = onKey;
            Invalidate();
        }

        public void StopRecording()
        {
            if (!recording) return;
            recording = false;
            if (owner.App.Hooks != null && owner.App.Hooks.Recorder == onKey) owner.App.Hooks.Recorder = null;
            Invalidate();
        }

        void OnKey(int mods, int vk)
        {
            if (!recording) return;
            bool fkey = vk >= 0x70 && vk <= 0x87;
            if (mods == 0 && vk == 0x1B) { StopRecording(); return; }
            if (mods == 0 && (vk == 0x08 || vk == 0x2E)) { owner.Assign(Action, Shortcut.Empty); StopRecording(); return; }
            if ((mods == 0 || mods == Mod.Shift) && !fkey) { System.Media.SystemSounds.Beep.Play(); return; }
            owner.Assign(Action, new Shortcut(mods, vk));
            StopRecording();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (!recording && !value.IsEmpty && ClearRect.Contains(e.Location)) { owner.Assign(Action, Shortcut.Empty); return; }
            if (recording) StopRecording(); else StartRecording();
        }

        protected override void OnLostFocus(EventArgs e) { StopRecording(); base.OnLostFocus(e); }
        protected override void OnMouseMove(MouseEventArgs e) { Invalidate(); base.OnMouseMove(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            UI.FillRound(g, recording ? P.Field : (hover ? P.SurfaceHover : P.Field), r, UI.S(5));
            if (recording) UI.StrokeRound(g, P.Accent, new RectangleF(1, 1, Width - 3, Height - 3), UI.S(5), Math.Max(1.5f, UI.S(1.6f)));
            else UI.StrokeRound(g, P.FieldBorder, r, UI.S(5), 1);

            string text; Color c;
            if (recording) { text = "Pulsa el atajo…"; c = P.Accent; }
            else if (value.IsEmpty) { text = "Grabar atajo"; c = P.SubText; }
            else { text = value.ToString(); c = P.Text; }
            var tr = new Rectangle(UI.S(6), 0, Width - UI.S(12) - (value.IsEmpty || recording ? 0 : Height - UI.S(6)), Height);
            UI.Text(g, text, Font, c, tr, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (!recording && !value.IsEmpty)
            {
                var cr = ClearRect;
                float d = Height * 0.5f;
                var circle = new RectangleF(cr.X + (cr.Width - d) / 2f, cr.Y + (cr.Height - d) / 2f, d, d);
                bool over = hover && cr.Contains(PointToClient(Cursor.Position));
                using (var b = new SolidBrush(over ? P.SubText : Color.FromArgb(P.Light ? 70 : 90, P.SubText))) g.FillEllipse(b, circle);
                float k = d * 0.28f, cx = circle.X + d / 2, cy = circle.Y + d / 2;
                using (var pen = new Pen(P.Field, Math.Max(1.2f, UI.S(1.4f))) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(pen, cx - k, cy - k, cx + k, cy + k);
                    g.DrawLine(pen, cx - k, cy + k, cx + k, cy - k);
                }
            }
        }
    }

    sealed class IconLabel : FlatControl
    {
        readonly ActionId action;
        public IconLabel(Palette p, ActionId a) : base(p) { action = a; Text = Actions.NameOf(a); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            int isz = UI.S(18);
            Icons.Draw(g, action, new RectangleF(0, (Height - isz) / 2f, isz, isz), P.SubText);
            int tx = isz + UI.S(10);
            UI.Text(g, Text, Font, P.Text, new Rectangle(tx, 0, Width - tx, Height), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    sealed class TabBar : FlatControl
    {
        public readonly string[] Labels = { "Atajos de teclado", "Áreas de ajuste", "Ajustes" };
        int selected, hot = -1;
        public event EventHandler SelectedChanged;
        public TabBar(Palette p) : base(p) { Cursor = Cursors.Hand; }
        public int Selected
        {
            get { return selected; }
            set { selected = value; Invalidate(); if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty); }
        }
        int TabW { get { return UI.S(132); } }
        Rectangle TabRect(int i)
        {
            int total = TabW * Labels.Length + UI.S(8) * (Labels.Length - 1);
            int x0 = (Width - total) / 2;
            return new Rectangle(x0 + i * (TabW + UI.S(8)), UI.S(8), TabW, Height - UI.S(16));
        }
        int HitTab(Point p) { for (int i = 0; i < Labels.Length; i++) if (TabRect(i).Contains(p)) return i; return -1; }
        protected override void OnMouseMove(MouseEventArgs e) { int h = HitTab(e.Location); if (h != hot) { hot = h; Invalidate(); } base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = -1; base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { int h = HitTab(e.Location); if (h >= 0 && h != selected) Selected = h; base.OnMouseDown(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            for (int i = 0; i < Labels.Length; i++)
            {
                var r = TabRect(i);
                if (i == selected) { UI.FillRound(g, P.Surface, r, UI.S(6)); UI.StrokeRound(g, P.Border, r, UI.S(6), 1); }
                else if (i == hot) UI.FillRound(g, Color.FromArgb(P.Light ? 10 : 14, P.Text), r, UI.S(6));
                Color c = i == selected ? P.Accent : P.SubText;
                int isz = UI.S(24);
                var ir = new RectangleF(r.X + (r.Width - isz) / 2f, r.Y + UI.S(9), isz, isz);
                DrawGlyph(g, i, ir, c);
                UI.Text(g, Labels[i], Font, i == selected ? P.Text : P.SubText, new Rectangle(r.X, (int)ir.Bottom + UI.S(4), r.Width, UI.S(20)), TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
            }
        }
        static void DrawGlyph(Graphics g, int i, RectangleF r, Color c)
        {
            if (i == 1) { Icons.Draw(g, ActionId.TopLeft, r, c); return; }
            string glyph = i == 0 ? "" : "";
            using (var f = new Font("Segoe MDL2 Assets", r.Height * (i == 0 ? 0.95f : 0.8f), GraphicsUnit.Pixel))
                TextRenderer.DrawText(g, glyph, f, Rectangle.Round(r), c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    // Dibujo de la pantalla con la zona y la huella del área seleccionada
    sealed class SnapDiagram : FlatControl
    {
        public SnapDir? Dir;
        public string Choice;
        public SnapDiagram(Palette p) : base(p) { }
        public RectangleF ScreenRect { get { return new RectangleF(UI.S(10), UI.S(10), Width - UI.S(20), Height - UI.S(44)); } }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            var bez = new RectangleF(1, 1, Width - 2, Height - UI.S(34));
            UI.FillRound(g, P.Light ? Color.FromArgb(40, 40, 44) : Color.FromArgb(18, 18, 20), bez, UI.S(10));
            var scr = ScreenRect;
            using (var br = new LinearGradientBrush(scr, P.Light ? Color.FromArgb(120, 150, 230) : Color.FromArgb(52, 66, 120), P.Light ? Color.FromArgb(185, 140, 230) : Color.FromArgb(96, 62, 128), 35f))
            using (var path = Theme.Rounded(scr, UI.S(4))) g.FillPath(br, path);
            // pie del monitor
            float sw = UI.S(70);
            var stand = new RectangleF(Width / 2f - sw / 2, bez.Bottom, sw, UI.S(20));
            using (var b = new SolidBrush(P.Light ? Color.FromArgb(170, 170, 176) : Color.FromArgb(70, 70, 76))) g.FillRectangle(b, stand);
            UI.FillRound(g, P.Light ? Color.FromArgb(150, 150, 156) : Color.FromArgb(84, 84, 90), new RectangleF(Width / 2f - UI.S(60), stand.Bottom - 1, UI.S(120), UI.S(8)), UI.S(3));

            if (Dir == null || Choice == null || Choice == SnapChoices.None) return;
            ActionId a = ActionId.None;
            switch (Choice)
            {
                case SnapChoices.LeftCompound: a = ActionId.LeftHalf; break;
                case SnapChoices.RightCompound: a = ActionId.RightHalf; break;
                case SnapChoices.ThirdsCompound: a = ActionId.FirstThird; break;
                default: Enum.TryParse(Choice, out a); break;
            }
            var unit = new RECT(0, 0, 10000, 10000);
            RECT t = a == ActionId.AlmostMaximize ? Encuadre.Layout.AlmostMaximize(unit) : a == ActionId.MaximizeHeight ? new RECT(3000, 0, 7000, 10000) : a == ActionId.Center ? new RECT(2500, 2000, 7500, 8000) : Encuadre.Layout.Tile(a, unit, false, 0);
            float pad = UI.S(5);
            var fr = new RectangleF(scr.X + scr.Width * t.Left / 10000f + pad, scr.Y + scr.Height * t.Top / 10000f + pad,
                                    scr.Width * t.Width / 10000f - 2 * pad, scr.Height * t.Height / 10000f - 2 * pad);
            UI.FillRound(g, Color.FromArgb(90, 255, 255, 255), fr, UI.S(6));
            UI.StrokeRound(g, Color.FromArgb(220, 255, 255, 255), fr, UI.S(6), Math.Max(1f, UI.S(1.5f)));
            if (Choice == SnapChoices.ThirdsCompound)
            {
                using (var pen = new Pen(Color.FromArgb(160, 255, 255, 255), 1) { DashStyle = DashStyle.Dash })
                {
                    g.DrawLine(pen, scr.X + scr.Width * 2 / 3f, scr.Y + pad, scr.X + scr.Width * 2 / 3f, scr.Bottom - pad);
                }
            }
            // zona de activación en el borde
            float z = UI.S(5), c = UI.S(26);
            var zones = new List<RectangleF>();
            switch (Dir.Value)
            {
                case SnapDir.T: zones.Add(new RectangleF(scr.X + c, scr.Y, scr.Width - 2 * c, z)); break;
                case SnapDir.B: zones.Add(new RectangleF(scr.X + c, scr.Bottom - z, scr.Width - 2 * c, z)); break;
                case SnapDir.L: zones.Add(new RectangleF(scr.X, scr.Y + c, z, scr.Height - 2 * c)); break;
                case SnapDir.R: zones.Add(new RectangleF(scr.Right - z, scr.Y + c, z, scr.Height - 2 * c)); break;
                case SnapDir.TL: zones.Add(new RectangleF(scr.X, scr.Y, c, z)); zones.Add(new RectangleF(scr.X, scr.Y, z, c)); break;
                case SnapDir.TR: zones.Add(new RectangleF(scr.Right - c, scr.Y, c, z)); zones.Add(new RectangleF(scr.Right - z, scr.Y, z, c)); break;
                case SnapDir.BL: zones.Add(new RectangleF(scr.X, scr.Bottom - z, c, z)); zones.Add(new RectangleF(scr.X, scr.Bottom - c, z, c)); break;
                case SnapDir.BR: zones.Add(new RectangleF(scr.Right - c, scr.Bottom - z, c, z)); zones.Add(new RectangleF(scr.Right - z, scr.Bottom - c, z, c)); break;
            }
            using (var b = new SolidBrush(P.Accent)) foreach (var zr in zones) g.FillRectangle(b, zr);
        }
    }

    sealed class Choice
    {
        public string Key, Text;
        public Choice(string k, string t) { Key = k; Text = t; }
        public override string ToString() { return Text; }
    }

    public sealed class PrefsForm : Form
    {
        public readonly App App;
        readonly Palette P;
        TabBar tabs;
        readonly List<Control> pages = new List<Control>();
        readonly Dictionary<ActionId, ShortcutBox> boxes = new Dictionary<ActionId, ShortcutBox>();
        SnapDiagram diagram;
        ListBox ignoredList;
        Label tipLabel;
        LinkLabel tipLink;

        public PrefsForm(App app, int tab)
        {
            App = app;
            P = Palette.Create(Theme.AppsLight);
            Text = "Encuadre";
            Font = Theme.UiFont(9.75f);
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = P.Back;
            ForeColor = P.Text;
            KeyPreview = true;
            try { Icon = Icons.AppIcon(UI.S(32)); } catch { }

            var work = Screen.FromPoint(Cursor.Position).WorkingArea;
            int w = Math.Min(UI.S(860), work.Width - UI.S(40));
            int h = Math.Min(UI.S(720), work.Height - UI.S(60));
            ClientSize = new Size(w, h);
            Location = new Point(work.Left + (work.Width - Width) / 2, work.Top + (work.Height - Height) / 2);

            tabs = new TabBar(P) { Dock = DockStyle.Top, Height = UI.S(88) };
            var sep = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = P.Border };

            pages.Add(BuildShortcutsPage());
            pages.Add(BuildSnapPage());
            pages.Add(BuildSettingsPage());
            foreach (var p in pages) { p.Dock = DockStyle.Fill; p.Visible = false; Controls.Add(p); }
            Controls.Add(sep);
            Controls.Add(tabs);
            tabs.SelectedChanged += delegate { ShowPage(tabs.Selected); };
            tabs.Selected = Math.Max(0, Math.Min(2, tab));
        }

        public void SelectTab(int i) { tabs.Selected = Math.Max(0, Math.Min(2, i)); }

        void ShowPage(int i)
        {
            StopRecordingAll();
            for (int k = 0; k < pages.Count; k++) pages[k].Visible = k == i;
            Text = tabs.Labels[i] + " — Encuadre";
            if (i == 1) UpdateTip();
            if (i == 2) RefreshIgnored();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.SetDarkTitleBar(Handle, !P.Light);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            RefreshIgnored();
            UpdateTip();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            StopRecordingAll();
            base.OnFormClosed(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            foreach (var b in boxes.Values) if (b.Recording) return true; // el gancho ya recoge las teclas
            if (keyData == Keys.Escape) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public void StopRecordingAll() { foreach (var b in boxes.Values) b.StopRecording(); }

        public void Assign(ActionId a, Shortcut s)
        {
            if (!s.IsEmpty)
            {
                foreach (var kv in new List<KeyValuePair<ActionId, Shortcut>>(App.Config.Shortcuts))
                    if (kv.Key != a && kv.Value.Key == s.Key)
                    {
                        App.Config.Shortcuts[kv.Key] = Shortcut.Empty;
                        ShortcutBox other;
                        if (boxes.TryGetValue(kv.Key, out other)) other.Value = Shortcut.Empty;
                    }
            }
            App.Config.Shortcuts[a] = s;
            ShortcutBox box;
            if (boxes.TryGetValue(a, out box)) box.Value = s;
            App.Config.Save();
            App.ShortcutsChanged();
        }

        Panel NewPage()
        {
            var p = new DoubleBufferedPanel { AutoScroll = true, BackColor = P.Back };
            p.HandleCreated += delegate { if (!P.Light) Native.SetWindowTheme(p.Handle, "DarkMode_Explorer", null); };
            return p;
        }

        Label NewLabel(string text, int x, int y, int w, bool sub)
        {
            var l = new Label
            {
                Text = text, AutoSize = false, Location = new Point(x, y), Size = new Size(w, UI.S(22)),
                ForeColor = sub ? P.SubText : P.Text, BackColor = Color.Transparent, UseMnemonic = false
            };
            return l;
        }

        ComboBox NewCombo(int x, int y, int w)
        {
            var cb = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = P.Light ? FlatStyle.Flat : FlatStyle.Standard, DrawMode = DrawMode.OwnerDrawFixed,
                BackColor = P.Field, ForeColor = P.Text, Location = new Point(x, y), Width = w, ItemHeight = UI.S(22),
                IntegralHeight = false, MaxDropDownItems = 14
            };
            cb.DrawItem += delegate(object s, DrawItemEventArgs e)
            {
                if (e.Index < 0) return;
                bool sel = (e.State & DrawItemState.Selected) != 0;
                bool edit = (e.State & DrawItemState.ComboBoxEdit) != 0;
                using (var b = new SolidBrush(sel && !edit ? P.Accent : P.Field)) e.Graphics.FillRectangle(b, e.Bounds);
                TextRenderer.DrawText(e.Graphics, cb.Items[e.Index].ToString(), cb.Font,
                    new Rectangle(e.Bounds.X + UI.S(6), e.Bounds.Y, e.Bounds.Width - UI.S(8), e.Bounds.Height),
                    sel && !edit ? P.AccentText : P.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };
            cb.HandleCreated += delegate { if (!P.Light) Native.SetWindowTheme(cb.Handle, "DarkMode_CFD", null); };
            return cb;
        }

        // ---------- Atajos ----------
        Control BuildShortcutsPage()
        {
            var page = NewPage();
            int colW = (ClientSize.Width - UI.S(56) - UI.S(32) - SystemInformation.VerticalScrollBarWidth) / 2;
            int[][] cols = { new[] { 0, 1, 2, 6 }, new[] { 3, 4, 5, 7 } };
            int rowH = UI.S(34), boxW = Math.Min(UI.S(170), colW / 2), boxH = UI.S(28);
            int maxY = 0;
            for (int c = 0; c < 2; c++)
            {
                int x = UI.S(28) + c * (colW + UI.S(32));
                int y = UI.S(18);
                foreach (int grp in cols[c])
                {
                    foreach (var def in Actions.All)
                    {
                        if (def.Group != grp) continue;
                        var lbl = new IconLabel(P, def.Id) { Location = new Point(x, y), Size = new Size(colW - boxW - UI.S(8), rowH) };
                        var box = new ShortcutBox(P, this, def.Id, App.Config.ShortcutFor(def.Id))
                        {
                            Location = new Point(x + colW - boxW, y + (rowH - boxH) / 2), Size = new Size(boxW, boxH)
                        };
                        boxes[def.Id] = box;
                        page.Controls.Add(lbl);
                        page.Controls.Add(box);
                        y += rowH;
                    }
                    y += UI.S(14);
                }
                maxY = Math.Max(maxY, y);
            }
            var hint = NewLabel("Haz clic en un atajo y pulsa la nueva combinación. Retroceso lo borra, Esc cancela. " +
                                "AltGr nunca cuenta como Ctrl, así que € y @ siguen funcionando.", UI.S(28), maxY, ClientSize.Width - UI.S(80), true);
            hint.Height = UI.S(44);
            page.Controls.Add(hint);
            page.Controls.Add(new Label { Location = new Point(0, maxY + UI.S(60)), Size = new Size(1, 1) });
            return page;
        }

        // ---------- Áreas de ajuste ----------
        Control BuildSnapPage()
        {
            var page = NewPage();
            int x = UI.S(28), y = UI.S(18), fullW = ClientSize.Width - UI.S(56) - SystemInformation.VerticalScrollBarWidth;
            var cfg = App.Config;

            var snapOn = new FlatCheck(P, "Ajustar ventanas arrastrándolas a los bordes y esquinas de la pantalla", cfg.SnapEnabled) { Location = new Point(x, y), Width = fullW };
            snapOn.CheckedChanged += delegate { cfg.SnapEnabled = snapOn.Checked; cfg.Save(); App.SnapSettingsChanged(); };
            page.Controls.Add(snapOn); y += UI.S(32);

            var unsnap = new FlatCheck(P, "Recuperar el tamaño original al sacar la ventana de un área", cfg.RestoreOnUnsnap) { Location = new Point(x, y), Width = fullW };
            unsnap.CheckedChanged += delegate { cfg.RestoreOnUnsnap = unsnap.Checked; cfg.Save(); };
            page.Controls.Add(unsnap); y += UI.S(32);

            var anim = new FlatCheck(P, "Animar la vista previa", cfg.AnimateFootprint) { Location = new Point(x, y), Width = fullW };
            anim.CheckedChanged += delegate { cfg.AnimateFootprint = anim.Checked; cfg.Save(); };
            page.Controls.Add(anim); y += UI.S(44);

            // bloque: pantalla en el centro y desplegables alrededor
            int blockW = Math.Min(fullW, UI.S(780));
            int ox = x + (fullW - blockW) / 2, oy = y;
            int comboW = UI.S(196);
            int dW = blockW - 2 * comboW - UI.S(40), dH = (int)(dW * 0.62f) + UI.S(34);
            int dX = ox + comboW + UI.S(20), dY = oy + UI.S(40);
            diagram = new SnapDiagram(P) { Location = new Point(dX, dY), Size = new Size(dW, dH) };
            page.Controls.Add(diagram);
            int scrTop = dY + UI.S(10), scrBottom = dY + dH - UI.S(34), scrMid = (scrTop + scrBottom) / 2;
            int ch = UI.S(28);
            var pos = new Dictionary<SnapDir, Point>
            {
                { SnapDir.TL, new Point(ox, scrTop - ch / 2) },
                { SnapDir.T, new Point(dX + (dW - comboW) / 2, oy) },
                { SnapDir.TR, new Point(ox + blockW - comboW, scrTop - ch / 2) },
                { SnapDir.L, new Point(ox, scrMid - ch / 2) },
                { SnapDir.R, new Point(ox + blockW - comboW, scrMid - ch / 2) },
                { SnapDir.BL, new Point(ox, scrBottom - ch / 2) },
                { SnapDir.B, new Point(dX + (dW - comboW) / 2, dY + dH + UI.S(10)) },
                { SnapDir.BR, new Point(ox + blockW - comboW, scrBottom - ch / 2) },
            };
            foreach (var kv in pos)
            {
                SnapDir d = kv.Key;
                var cb = NewCombo(kv.Value.X, kv.Value.Y, comboW);
                string cur;
                if (!cfg.SnapAreas.TryGetValue(d, out cur)) cur = SnapChoices.Default(d);
                foreach (var opt in SnapChoices.OptionsFor(d))
                {
                    int i = cb.Items.Add(new Choice(opt, SnapChoices.Label(opt)));
                    if (opt == cur) cb.SelectedIndex = i;
                }
                if (cb.SelectedIndex < 0) cb.SelectedIndex = 0;
                cb.SelectedIndexChanged += delegate
                {
                    var c = (Choice)cb.SelectedItem;
                    cfg.SnapAreas[d] = c.Key;
                    cfg.Save();
                    diagram.Dir = d; diagram.Choice = c.Key; diagram.Invalidate();
                };
                EventHandler show = delegate
                {
                    diagram.Dir = d; diagram.Choice = ((Choice)cb.SelectedItem).Key; diagram.Invalidate();
                };
                cb.MouseEnter += show;
                cb.Enter += show;
                page.Controls.Add(cb);
            }
            diagram.Dir = SnapDir.L; diagram.Choice = cfg.SnapAreas.ContainsKey(SnapDir.L) ? cfg.SnapAreas[SnapDir.L] : SnapChoices.LeftCompound;

            y = dY + dH + UI.S(56);
            tipLabel = NewLabel("Windows tiene su propio «Ajustar ventanas», que también reacciona al arrastrar a los bordes. " +
                                "Para que sea idéntico a Rectangle y no salgan dos vistas previas, desactívalo en Configuración › Sistema › Multitarea.",
                                x, y, fullW, true);
            tipLabel.Height = UI.S(44);
            page.Controls.Add(tipLabel);
            tipLink = new LinkLabel
            {
                Text = "Abrir Configuración › Multitarea", Location = new Point(x, y + UI.S(46)), AutoSize = true,
                LinkColor = P.Accent, ActiveLinkColor = P.Accent, VisitedLinkColor = P.Accent, BackColor = Color.Transparent
            };
            tipLink.LinkClicked += delegate { try { Process.Start("ms-settings:multitasking"); } catch { } };
            page.Controls.Add(tipLink);
            page.Controls.Add(new Label { Location = new Point(0, y + UI.S(90)), Size = new Size(1, 1) });
            UpdateTip();
            return page;
        }

        void UpdateTip()
        {
            if (tipLabel == null) return;
            bool on = Theme.WindowsSnapEnabled;
            tipLabel.Visible = on;
            tipLink.Visible = on;
        }

        // ---------- Ajustes ----------
        Control BuildSettingsPage()
        {
            var page = NewPage();
            var cfg = App.Config;
            int x = UI.S(28), y = UI.S(18), fullW = ClientSize.Width - UI.S(56) - SystemInformation.VerticalScrollBarWidth;

            var login = new FlatCheck(P, "Abrir Encuadre al iniciar sesión en Windows", App.LaunchAtLogin) { Location = new Point(x, y), Width = fullW };
            login.CheckedChanged += delegate { App.LaunchAtLogin = login.Checked; };
            page.Controls.Add(login); y += UI.S(32);

            var hide = new FlatCheck(P, "Ocultar el icono de la bandeja (vuelve a abrir Encuadre para ver estas preferencias)", cfg.HideTrayIcon) { Location = new Point(x, y), Width = fullW };
            hide.CheckedChanged += delegate { cfg.HideTrayIcon = hide.Checked; cfg.Save(); App.TrayVisibilityChanged(); };
            page.Controls.Add(hide); y += UI.S(48);

            int labelW = UI.S(230);
            page.Controls.Add(NewLabel("Comandos repetidos", x, y + UI.S(4), labelW, false));
            var rep = NewCombo(x + labelW, y, UI.S(380));
            rep.Items.Add(new Choice("0", "Alternar ½, ⅔ y ⅓ en las mitades"));
            rep.Items.Add(new Choice("1", "Mover a la pantalla contigua (izq./der.)"));
            rep.Items.Add(new Choice("2", "No hacer nada"));
            rep.SelectedIndex = Math.Max(0, Math.Min(2, cfg.RepeatMode));
            rep.SelectedIndexChanged += delegate { cfg.RepeatMode = rep.SelectedIndex; cfg.Save(); };
            page.Controls.Add(rep); y += UI.S(46);

            page.Controls.Add(NewLabel("Espacio entre ventanas", x, y + UI.S(4), labelW, false));
            var gapVal = NewLabel(cfg.Gap + " px", x + labelW + UI.S(310), y + UI.S(4), UI.S(70), true);
            var gap = new Slider(P, 50, cfg.Gap) { Location = new Point(x + labelW - UI.S(8), y), Width = UI.S(310) };
            gap.ValueChanged += delegate { cfg.Gap = gap.Value; gapVal.Text = gap.Value + " px"; cfg.Save(); };
            page.Controls.Add(gap);
            page.Controls.Add(gapVal); y += UI.S(52);

            page.Controls.Add(NewLabel("Apps ignoradas", x, y, labelW, false));
            page.Controls.Add(NewLabel("Sus atajos no los captura Encuadre. Añádelas desde el menú de la bandeja.", x, y + UI.S(22), labelW, true));
            page.Controls[page.Controls.Count - 1].Height = UI.S(64);
            var listHost = new Panel { Location = new Point(x + labelW, y), Size = new Size(UI.S(380), UI.S(96)), BackColor = P.FieldBorder, Padding = new Padding(1) };
            ignoredList = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = P.Field, ForeColor = P.Text, IntegralHeight = false };
            listHost.Controls.Add(ignoredList);
            page.Controls.Add(listHost);
            var remove = new FlatButton(P, "Dejar de ignorar") { Location = new Point(x + labelW + UI.S(392), y) };
            remove.Click += delegate
            {
                if (ignoredList.SelectedItem == null) return;
                lock (cfg.Ignored) cfg.Ignored.Remove((string)ignoredList.SelectedItem);
                cfg.Save();
                RefreshIgnored();
            };
            page.Controls.Add(remove);
            y += UI.S(120);

            var sep = new Panel { Location = new Point(x, y), Size = new Size(fullW, 1), BackColor = P.Border };
            page.Controls.Add(sep); y += UI.S(18);

            var resetKeys = new FlatButton(P, "Restaurar atajos por defecto") { Location = new Point(x, y) };
            resetKeys.Click += delegate
            {
                cfg.ResetShortcuts(); cfg.Save(); App.ShortcutsChanged();
                foreach (var kv in boxes) kv.Value.Value = cfg.ShortcutFor(kv.Key);
            };
            page.Controls.Add(resetKeys);
            var resetSnap = new FlatButton(P, "Restaurar áreas por defecto") { Location = new Point(resetKeys.Right + UI.S(10), y) };
            resetSnap.Click += delegate
            {
                cfg.ResetSnapAreas(); cfg.Save();
                var old = pages[1];
                var np = BuildSnapPage();
                np.Dock = DockStyle.Fill;
                np.Visible = false;
                pages[1] = np;
                Controls.Add(np);
                Controls.SetChildIndex(np, 0);
                Controls.Remove(old);
                old.Dispose();
            };
            page.Controls.Add(resetSnap);
            var quit = new FlatButton(P, "Salir de Encuadre") { Location = new Point(x + fullW - UI.S(10), y) };
            quit.Left = x + fullW - quit.Width;
            quit.Click += delegate { App.Quit(); };
            page.Controls.Add(quit);
            y += UI.S(56);

            var about = NewLabel("Encuadre 1.0 · gestor de ventanas para Windows, inspirado en Rectangle para macOS", x, y, fullW, true);
            page.Controls.Add(about);
            page.Controls.Add(new Label { Location = new Point(0, y + UI.S(40)), Size = new Size(1, 1) });
            return page;
        }

        void RefreshIgnored()
        {
            if (ignoredList == null) return;
            ignoredList.Items.Clear();
            lock (App.Config.Ignored) foreach (var s in App.Config.Ignored) ignoredList.Items.Add(s);
        }
    }

    class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel() { DoubleBuffered = true; }
    }
}
