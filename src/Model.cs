using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Encuadre
{
    public enum ActionId
    {
        None = -1,
        LeftHalf, RightHalf, CenterHalf, TopHalf, BottomHalf,
        TopLeft, TopRight, BottomLeft, BottomRight,
        FirstThird, CenterThird, LastThird, FirstTwoThirds, CenterTwoThirds, LastTwoThirds,
        Maximize, AlmostMaximize, MaximizeHeight, Smaller, Larger, Center, Restore,
        NextDisplay, PreviousDisplay,
        MoveLeft, MoveRight, MoveUp, MoveDown,
        FirstFourth, SecondFourth, ThirdFourth, LastFourth, FirstThreeFourths, LastThreeFourths,
        TopLeftSixth, TopCenterSixth, TopRightSixth, BottomLeftSixth, BottomCenterSixth, BottomRightSixth
    }

    public static class Mod
    {
        public const int Ctrl = 1, Alt = 2, Shift = 4, Win = 8;
    }

    public struct Shortcut
    {
        public int Mods;
        public int Vk;
        public Shortcut(int mods, int vk) { Mods = mods; Vk = vk; }
        public bool IsEmpty { get { return Vk == 0; } }
        public long Key { get { return ((long)Mods << 16) | (uint)Vk; } }
        public static readonly Shortcut Empty = new Shortcut(0, 0);

        public string Serialize() { return IsEmpty ? "" : Mods + "," + Vk; }

        public static Shortcut Parse(string s)
        {
            if (string.IsNullOrEmpty(s)) return Empty;
            string[] p = s.Split(',');
            int m, v;
            if (p.Length == 2 && int.TryParse(p[0], out m) && int.TryParse(p[1], out v)) return new Shortcut(m, v);
            return Empty;
        }

        public override string ToString()
        {
            if (IsEmpty) return "";
            var sb = new StringBuilder();
            if ((Mods & Mod.Win) != 0) sb.Append("Win+");
            if ((Mods & Mod.Ctrl) != 0) sb.Append("Ctrl+");
            if ((Mods & Mod.Alt) != 0) sb.Append("Alt+");
            if ((Mods & Mod.Shift) != 0) sb.Append("Mayús+");
            sb.Append(KeyName(Vk));
            return sb.ToString();
        }

        public static string KeyName(int vk)
        {
            switch (vk)
            {
                case 0x25: return "←";
                case 0x26: return "↑";
                case 0x27: return "→";
                case 0x28: return "↓";
                case 0x0D: return "Intro";
                case 0x08: return "Retroceso";
                case 0x2E: return "Supr";
                case 0x2D: return "Insert";
                case 0x20: return "Espacio";
                case 0x09: return "Tab";
                case 0x1B: return "Esc";
                case 0x24: return "Inicio";
                case 0x23: return "Fin";
                case 0x21: return "RePág";
                case 0x22: return "AvPág";
                case 0x6A: return "Num *";
                case 0x6B: return "Num +";
                case 0x6D: return "Num -";
                case 0x6E: return "Num ,";
                case 0x6F: return "Num /";
            }
            if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x6F);
            if ((vk >= 0x30 && vk <= 0x39) || (vk >= 0x41 && vk <= 0x5A)) return ((char)vk).ToString();
            if (vk >= 0x60 && vk <= 0x69) return "Num " + (vk - 0x60);
            uint ch = Native.MapVirtualKey((uint)vk, 2);
            if (ch != 0)
            {
                char c = (char)(ch & 0x7FFF);
                if (!char.IsControl(c)) return c.ToString().ToUpperInvariant();
            }
            return "0x" + vk.ToString("X2");
        }
    }

    public sealed class ActionDef
    {
        public ActionId Id;
        public string Name;
        public Shortcut Default;
        public int Group;
        public ActionDef(ActionId id, string name, int group, int mods, int vk)
        {
            Id = id; Name = name; Group = group; Default = new Shortcut(mods, vk);
        }
    }

    public static class Actions
    {
        const int CA = Mod.Win | Mod.Alt; // Win+Alt hace el papel de ⌃⌥ de macOS
        const int VK_LEFT = 0x25, VK_UP = 0x26, VK_RIGHT = 0x27, VK_DOWN = 0x28, VK_RETURN = 0x0D, VK_BACK = 0x08;
        const int VK_OEM_MINUS = 0xBD, VK_OEM_PLUS = 0xBB;

        // Teclas por defecto de Rectangle, con Win+Alt (⌘⌥) como modificador base; pantallas: Ctrl+Alt+Win
        public static readonly ActionDef[] All = new ActionDef[]
        {
            new ActionDef(ActionId.LeftHalf, "Mitad izquierda", 0, CA, VK_LEFT),
            new ActionDef(ActionId.RightHalf, "Mitad derecha", 0, CA, VK_RIGHT),
            new ActionDef(ActionId.CenterHalf, "Mitad central", 0, 0, 0),
            new ActionDef(ActionId.TopHalf, "Mitad superior", 0, CA, VK_UP),
            new ActionDef(ActionId.BottomHalf, "Mitad inferior", 0, CA, VK_DOWN),

            new ActionDef(ActionId.TopLeft, "Arriba a la izquierda", 1, CA, 'U'),
            new ActionDef(ActionId.TopRight, "Arriba a la derecha", 1, CA, 'I'),
            new ActionDef(ActionId.BottomLeft, "Abajo a la izquierda", 1, CA, 'J'),
            new ActionDef(ActionId.BottomRight, "Abajo a la derecha", 1, CA, 'K'),

            new ActionDef(ActionId.FirstThird, "Primer tercio", 2, CA, 'D'),
            new ActionDef(ActionId.CenterThird, "Tercio central", 2, CA, 'F'),
            new ActionDef(ActionId.LastThird, "Último tercio", 2, CA, 'G'),
            new ActionDef(ActionId.FirstTwoThirds, "Primeros dos tercios", 2, CA, 'E'),
            new ActionDef(ActionId.CenterTwoThirds, "Dos tercios centrales", 2, CA, 'R'),
            new ActionDef(ActionId.LastTwoThirds, "Últimos dos tercios", 2, CA, 'T'),

            new ActionDef(ActionId.Maximize, "Maximizar", 3, CA, VK_RETURN),
            new ActionDef(ActionId.AlmostMaximize, "Casi maximizar", 3, 0, 0),
            new ActionDef(ActionId.MaximizeHeight, "Maximizar altura", 3, CA | Mod.Shift, VK_UP),
            new ActionDef(ActionId.Smaller, "Más pequeña", 3, CA, VK_OEM_MINUS),
            new ActionDef(ActionId.Larger, "Más grande", 3, CA, VK_OEM_PLUS),
            new ActionDef(ActionId.Center, "Centrar", 3, CA, 'C'),
            new ActionDef(ActionId.Restore, "Restaurar", 3, CA, VK_BACK),

            new ActionDef(ActionId.NextDisplay, "Pantalla siguiente", 4, CA | Mod.Ctrl, VK_RIGHT),
            new ActionDef(ActionId.PreviousDisplay, "Pantalla anterior", 4, CA | Mod.Ctrl, VK_LEFT),

            new ActionDef(ActionId.MoveLeft, "Mover a la izquierda", 5, 0, 0),
            new ActionDef(ActionId.MoveRight, "Mover a la derecha", 5, 0, 0),
            new ActionDef(ActionId.MoveUp, "Mover arriba", 5, 0, 0),
            new ActionDef(ActionId.MoveDown, "Mover abajo", 5, 0, 0),

            new ActionDef(ActionId.FirstFourth, "Primer cuarto", 6, 0, 0),
            new ActionDef(ActionId.SecondFourth, "Segundo cuarto", 6, 0, 0),
            new ActionDef(ActionId.ThirdFourth, "Tercer cuarto", 6, 0, 0),
            new ActionDef(ActionId.LastFourth, "Último cuarto", 6, 0, 0),
            new ActionDef(ActionId.FirstThreeFourths, "Primeros tres cuartos", 6, 0, 0),
            new ActionDef(ActionId.LastThreeFourths, "Últimos tres cuartos", 6, 0, 0),

            new ActionDef(ActionId.TopLeftSixth, "Sexto superior izquierdo", 7, 0, 0),
            new ActionDef(ActionId.TopCenterSixth, "Sexto superior central", 7, 0, 0),
            new ActionDef(ActionId.TopRightSixth, "Sexto superior derecho", 7, 0, 0),
            new ActionDef(ActionId.BottomLeftSixth, "Sexto inferior izquierdo", 7, 0, 0),
            new ActionDef(ActionId.BottomCenterSixth, "Sexto inferior central", 7, 0, 0),
            new ActionDef(ActionId.BottomRightSixth, "Sexto inferior derecho", 7, 0, 0),
        };

        public static ActionDef Get(ActionId id)
        {
            foreach (var a in All) if (a.Id == id) return a;
            return null;
        }

        public static string NameOf(ActionId id)
        {
            var d = Get(id);
            return d == null ? id.ToString() : d.Name;
        }

        public static bool Repeatable(ActionId id)
        {
            return id == ActionId.Smaller || id == ActionId.Larger || id == ActionId.MoveLeft ||
                   id == ActionId.MoveRight || id == ActionId.MoveUp || id == ActionId.MoveDown;
        }
    }

    // Zonas de ajuste (como en Rectangle): esquinas y bordes de la pantalla
    public enum SnapDir { TL, T, TR, L, R, BL, B, BR }

    public static class SnapChoices
    {
        public const string None = "none";
        public const string LeftCompound = "c:left";    // mitad izq. (sup./inf. cerca de las esquinas)
        public const string RightCompound = "c:right";
        public const string ThirdsCompound = "c:thirds"; // tercios a lo largo del borde

        public static string Label(string choice)
        {
            switch (choice)
            {
                case None: return "Ninguna";
                case LeftCompound: return "Mitad izq. + sup./inf.";
                case RightCompound: return "Mitad der. + sup./inf.";
                case ThirdsCompound: return "Tercios";
            }
            ActionId id;
            if (Enum.TryParse(choice, out id)) return Actions.NameOf(id);
            return choice;
        }

        public static readonly ActionId[] SnapActions = new ActionId[]
        {
            ActionId.Maximize, ActionId.AlmostMaximize, ActionId.MaximizeHeight, ActionId.Center,
            ActionId.LeftHalf, ActionId.RightHalf, ActionId.CenterHalf, ActionId.TopHalf, ActionId.BottomHalf,
            ActionId.TopLeft, ActionId.TopRight, ActionId.BottomLeft, ActionId.BottomRight,
            ActionId.FirstThird, ActionId.CenterThird, ActionId.LastThird,
            ActionId.FirstTwoThirds, ActionId.CenterTwoThirds, ActionId.LastTwoThirds,
            ActionId.FirstFourth, ActionId.SecondFourth, ActionId.ThirdFourth, ActionId.LastFourth,
            ActionId.TopLeftSixth, ActionId.TopCenterSixth, ActionId.TopRightSixth,
            ActionId.BottomLeftSixth, ActionId.BottomCenterSixth, ActionId.BottomRightSixth,
        };

        public static List<string> OptionsFor(SnapDir d)
        {
            var l = new List<string>();
            l.Add(None);
            if (d == SnapDir.L) l.Add(LeftCompound);
            if (d == SnapDir.R) l.Add(RightCompound);
            if (d == SnapDir.B || d == SnapDir.T) l.Add(ThirdsCompound);
            foreach (var a in SnapActions) l.Add(a.ToString());
            return l;
        }

        public static string Default(SnapDir d)
        {
            switch (d)
            {
                case SnapDir.TL: return ActionId.TopLeft.ToString();
                case SnapDir.T: return ActionId.Maximize.ToString();
                case SnapDir.TR: return ActionId.TopRight.ToString();
                case SnapDir.L: return LeftCompound;
                case SnapDir.R: return RightCompound;
                case SnapDir.BL: return ActionId.BottomLeft.ToString();
                case SnapDir.B: return ThirdsCompound;
                default: return ActionId.BottomRight.ToString();
            }
        }
    }

    public sealed class Config
    {
        public Dictionary<ActionId, Shortcut> Shortcuts = new Dictionary<ActionId, Shortcut>();
        public Dictionary<SnapDir, string> SnapAreas = new Dictionary<SnapDir, string>();
        public List<string> Ignored = new List<string>();
        public int Gap = 0;
        public bool SnapEnabled = true;
        public bool RestoreOnUnsnap = true;
        public bool AnimateFootprint = true;
        public bool HideTrayIcon = false;
        public int RepeatMode = 0; // 0 = alternar ½ ⅔ ⅓, 1 = mover a pantalla contigua, 2 = nada
        public bool FirstRun = true;
        public bool InMemory = false;

        public static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Encuadre"); }
        }
        public static string FilePath { get { return Path.Combine(Dir, "config.ini"); } }

        public static Config Defaults()
        {
            var c = new Config();
            c.ResetShortcuts();
            c.ResetSnapAreas();
            return c;
        }

        public void ResetShortcuts()
        {
            Shortcuts.Clear();
            foreach (var a in Actions.All) Shortcuts[a.Id] = a.Default;
        }

        public void ResetSnapAreas()
        {
            SnapAreas.Clear();
            foreach (SnapDir d in Enum.GetValues(typeof(SnapDir))) SnapAreas[d] = SnapChoices.Default(d);
        }

        public Shortcut ShortcutFor(ActionId id)
        {
            Shortcut s;
            return Shortcuts.TryGetValue(id, out s) ? s : Shortcut.Empty;
        }

        public bool IsIgnored(string exe)
        {
            if (string.IsNullOrEmpty(exe)) return false;
            lock (Ignored)
            {
                foreach (var i in Ignored) if (string.Equals(i, exe, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static Config Load()
        {
            var c = Defaults();
            try
            {
                if (!File.Exists(FilePath)) return c;
                c.FirstRun = false;
                int keysVersion = 1;
                foreach (var raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                    if (k.StartsWith("key."))
                    {
                        ActionId id;
                        if (Enum.TryParse(k.Substring(4), out id)) c.Shortcuts[id] = Shortcut.Parse(v);
                    }
                    else if (k.StartsWith("snap."))
                    {
                        SnapDir d;
                        if (Enum.TryParse(k.Substring(5), out d)) c.SnapAreas[d] = v;
                    }
                    else switch (k)
                    {
                        case "keysVersion": int.TryParse(v, out keysVersion); break;
                        case "gap": int.TryParse(v, out c.Gap); break;
                        case "snapEnabled": c.SnapEnabled = v == "1"; break;
                        case "restoreOnUnsnap": c.RestoreOnUnsnap = v == "1"; break;
                        case "animateFootprint": c.AnimateFootprint = v == "1"; break;
                        case "hideTrayIcon": c.HideTrayIcon = v == "1"; break;
                        case "repeatMode": int.TryParse(v, out c.RepeatMode); break;
                        case "ignored":
                            c.Ignored.Clear();
                            foreach (var s in v.Split('|')) if (s.Trim().Length > 0) c.Ignored.Add(s.Trim());
                            break;
                    }
                }
                if (keysVersion < 2)
                {
                    // v1 usaba Ctrl+Alt como base: los atajos que sigan siendo los de fábrica pasan a Win+Alt
                    foreach (var a in Actions.All)
                    {
                        if ((a.Default.Mods & Mod.Ctrl) != 0 || a.Default.IsEmpty) continue;
                        var old = new Shortcut((a.Default.Mods & ~Mod.Win) | Mod.Ctrl, a.Default.Vk);
                        if (c.ShortcutFor(a.Id).Key == old.Key) c.Shortcuts[a.Id] = a.Default;
                    }
                }
                if (keysVersion < 3)
                {
                    // v3: "Dos tercios centrales" recibe su atajo de Rectangle (R) si está libre
                    var d = Actions.Get(ActionId.CenterTwoThirds).Default;
                    bool used = false;
                    foreach (var kv in c.Shortcuts) if (kv.Value.Key == d.Key) used = true;
                    if (!used && c.ShortcutFor(ActionId.CenterTwoThirds).IsEmpty) c.Shortcuts[ActionId.CenterTwoThirds] = d;
                }
                if (keysVersion < KeysVersion) c.Save();
            }
            catch (Exception ex) { Log.Write("Config.Load", ex); }
            return c;
        }

        public const int KeysVersion = 3;

        public void Save()
        {
            if (InMemory) return;
            try
            {
                Directory.CreateDirectory(Dir);
                var sb = new StringBuilder();
                sb.AppendLine("# Encuadre - preferencias");
                sb.AppendLine("keysVersion=" + KeysVersion);
                sb.AppendLine("gap=" + Gap);
                sb.AppendLine("snapEnabled=" + (SnapEnabled ? 1 : 0));
                sb.AppendLine("restoreOnUnsnap=" + (RestoreOnUnsnap ? 1 : 0));
                sb.AppendLine("animateFootprint=" + (AnimateFootprint ? 1 : 0));
                sb.AppendLine("hideTrayIcon=" + (HideTrayIcon ? 1 : 0));
                sb.AppendLine("repeatMode=" + RepeatMode);
                lock (Ignored) sb.AppendLine("ignored=" + string.Join("|", Ignored.ToArray()));
                foreach (var a in Actions.All) sb.AppendLine("key." + a.Id + "=" + ShortcutFor(a.Id).Serialize());
                foreach (var kv in SnapAreas) sb.AppendLine("snap." + kv.Key + "=" + kv.Value);
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex) { Log.Write("Config.Save", ex); }
        }
    }

    public static class Log
    {
        public static void Write(string where, Exception ex) { Write(where + ": " + ex); }
        public static void Write(string msg)
        {
            try
            {
                Directory.CreateDirectory(Config.Dir);
                var p = Path.Combine(Config.Dir, "log.txt");
                var fi = new FileInfo(p);
                if (fi.Exists && fi.Length > 512 * 1024) fi.Delete();
                File.AppendAllText(p, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine);
            }
            catch { }
        }
    }
}
