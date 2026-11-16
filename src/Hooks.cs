using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Encuadre
{
    // Ganchos de teclado y ratón de bajo nivel en un hilo propio, para no frenar nunca la entrada del sistema.
    public sealed class InputHooks : IDisposable
    {
        const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12;
        const int VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1, VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3, VK_LMENU = 0xA4, VK_RMENU = 0xA5;
        const int VK_LWIN = 0x5B, VK_RWIN = 0x5C, VK_ESCAPE = 0x1B;
        const ushort MaskVk = 0xE8; // tecla "vacía" para que soltar Win/Alt no abra Inicio ni el menú de la app

        readonly Control invoker;
        Thread thread;
        uint threadId;
        IntPtr kbHook, msHook;
        HookProc kbProc, msProc;
        int suppressedVk;
        readonly ManualResetEvent ready = new ManualResetEvent(false);

        public volatile Dictionary<long, ActionId> Map = new Dictionary<long, ActionId>();
        public volatile bool HotkeysEnabled = true;
        public volatile bool MouseEnabled = true;
        public volatile Action<int, int> Recorder; // (mods, vk) mientras se graba un atajo
        public Func<IntPtr, bool> IsIgnored;
        public Action<ActionId, IntPtr> OnHotkey;
        public Action<int, int> OnLeftDown, OnLeftUp;

        public InputHooks(Control invoker) { this.invoker = invoker; }

        public void Start()
        {
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Name = "Encuadre hooks";
            thread.Start();
            ready.WaitOne(3000);
        }

        void Run()
        {
            threadId = Native.GetCurrentThreadId();
            kbProc = KeyboardProc;
            msProc = MouseProc;
            IntPtr mod = Native.GetModuleHandle(null);
            kbHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, kbProc, mod, 0);
            msHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, msProc, mod, 0);
            if (kbHook == IntPtr.Zero || msHook == IntPtr.Zero) Log.Write("No se pudieron instalar los ganchos: " + Marshal.GetLastWin32Error());
            ready.Set();
            MSG msg;
            while (Native.GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
            {
                Native.TranslateMessage(ref msg);
                Native.DispatchMessage(ref msg);
            }
            if (kbHook != IntPtr.Zero) Native.UnhookWindowsHookEx(kbHook);
            if (msHook != IntPtr.Zero) Native.UnhookWindowsHookEx(msHook);
        }

        public void Dispose()
        {
            if (thread != null && threadId != 0) Native.PostThreadMessage(threadId, Native.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }

        static bool IsModifier(int vk)
        {
            switch (vk)
            {
                case VK_SHIFT: case VK_CONTROL: case VK_MENU:
                case VK_LSHIFT: case VK_RSHIFT: case VK_LCONTROL: case VK_RCONTROL: case VK_LMENU: case VK_RMENU:
                case VK_LWIN: case VK_RWIN:
                    return true;
            }
            return false;
        }

        // AltGr (Alt derecho en teclados españoles) genera un Ctrl izquierdo falso: se descarta ese Ctrl,
        // así AltGr+E sigue escribiendo € y AltGr+2 sigue escribiendo @, y Win+AltGr funciona como Win+Alt.
        public static int CurrentMods()
        {
            bool lctrl = Native.KeyDown(VK_LCONTROL), rctrl = Native.KeyDown(VK_RCONTROL);
            bool lalt = Native.KeyDown(VK_LMENU), ralt = Native.KeyDown(VK_RMENU);
            int m = 0;
            bool altGr = ralt && !lalt && lctrl;
            if (rctrl || (lctrl && !altGr)) m |= Mod.Ctrl;
            if (lalt || ralt) m |= Mod.Alt;
            if (Native.KeyDown(VK_LSHIFT) || Native.KeyDown(VK_RSHIFT)) m |= Mod.Shift;
            if (Native.KeyDown(VK_LWIN) || Native.KeyDown(VK_RWIN)) m |= Mod.Win;
            return m;
        }

        static void SendMask()
        {
            var inputs = new INPUT[2];
            inputs[0].type = Native.INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = MaskVk;
            inputs[1].type = Native.INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = MaskVk;
            inputs[1].U.ki.dwFlags = Native.KEYEVENTF_KEYUP;
            Native.SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0)
                {
                    var k = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                    int msg = wParam.ToInt32();
                    int vk = (int)k.vkCode;
                    bool down = msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN;
                    bool up = msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP;
                    if (vk == MaskVk || IsModifier(vk))
                        return Native.CallNextHookEx(kbHook, nCode, wParam, lParam);

                    if (down)
                    {
                        var rec = Recorder;
                        if (rec != null)
                        {
                            int mods = CurrentMods();
                            invoker.BeginInvoke(rec, mods, vk);
                            suppressedVk = vk;
                            if ((mods & (Mod.Alt | Mod.Win)) != 0) SendMask();
                            return (IntPtr)1;
                        }
                        if (suppressedVk == vk && suppressedVk != 0)
                        {
                            // tecla mantenida: solo repiten las acciones de tamaño/movimiento
                            ActionId held;
                            if (Map.TryGetValue(new Shortcut(CurrentMods(), vk).Key, out held) && Actions.Repeatable(held))
                                invoker.BeginInvoke(OnHotkey, held, Win.Root(Native.GetForegroundWindow()));
                            return (IntPtr)1;
                        }
                        if (HotkeysEnabled)
                        {
                            int mods = CurrentMods();
                            ActionId a;
                            if (mods != 0 && Map.TryGetValue(new Shortcut(mods, vk).Key, out a))
                            {
                                IntPtr fg = Win.Root(Native.GetForegroundWindow());
                                if (IsIgnored == null || !IsIgnored(fg))
                                {
                                    suppressedVk = vk;
                                    if ((mods & (Mod.Alt | Mod.Win)) != 0) SendMask();
                                    invoker.BeginInvoke(OnHotkey, a, fg);
                                    return (IntPtr)1;
                                }
                            }
                        }
                    }
                    else if (up && vk == suppressedVk)
                    {
                        suppressedVk = 0;
                        return (IntPtr)1;
                    }
                }
            }
            catch (Exception ex) { Log.Write("KeyboardProc", ex); }
            return Native.CallNextHookEx(kbHook, nCode, wParam, lParam);
        }

        IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && MouseEnabled)
                {
                    int msg = wParam.ToInt32();
                    if (msg == Native.WM_LBUTTONDOWN || msg == Native.WM_LBUTTONUP)
                    {
                        var m = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                        var cb = msg == Native.WM_LBUTTONDOWN ? OnLeftDown : OnLeftUp;
                        if (cb != null) invoker.BeginInvoke(cb, m.pt.X, m.pt.Y);
                    }
                }
            }
            catch (Exception ex) { Log.Write("MouseProc", ex); }
            return Native.CallNextHookEx(msHook, nCode, wParam, lParam);
        }
    }
}
