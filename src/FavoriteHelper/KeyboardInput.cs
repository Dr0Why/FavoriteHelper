using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace FavoriteHelper
{
    internal enum InputActionKind { ExplorerOpen, Favorite, Unfavorite, HookDiagnostic }
    internal sealed class InputAction
    {
        public readonly InputActionKind Kind;
        public readonly IntPtr Hwnd;
        public uint Vk;
        public bool Down, Up, Ctrl, Shift, Alt, Suppressed;
        public IntPtr CachedHwnd;
        public ForegroundKind CachedKind;
        public string Decision;
        public long ObservationVersion;
        public InputAction(InputActionKind kind, IntPtr hwnd) { Kind = kind; Hwnd = hwnd; }
    }

    internal sealed class KeyboardInput : IDisposable
    {
        private readonly Action<InputAction> enqueue;
        private readonly Thread thread;
        private readonly NativeMethods.HookProc callback;
        private volatile ForegroundKind foregroundKind;
        private volatile IntPtr foregroundHwnd;
        private volatile bool ctrl, shift, alt, handledF, handledU, handledP;
        private long observationVersion;
        private IntPtr hook;
        private uint nativeThreadId;
        private readonly Hotkey openHotkey, favoriteHotkey, unfavoriteHotkey;
        private volatile bool accepting = true;

        public KeyboardInput(Action<InputAction> enqueue, AppConfig config)
        {
            this.enqueue = enqueue;
            openHotkey = config.Open; favoriteHotkey = config.Favorite; unfavoriteHotkey = config.Unfavorite;
            callback = Hook;
            thread = new Thread(MessageLoop) { IsBackground = true, Name = "FavoriteHelper Keyboard Hook" };
            thread.SetApartmentState(ApartmentState.STA);
        }

        public void Start() { thread.Start(); }
        public void UpdateObservation(long version) { Interlocked.Exchange(ref observationVersion, version); }

        public void UpdateForeground(IntPtr hwnd, ForegroundKind kind, bool reconcile)
        {
            foregroundHwnd = hwnd;
            foregroundKind = hwnd == IntPtr.Zero ? ForegroundKind.Other : kind;
            if (!reconcile) return;
            ctrl = IsPhysicallyDown(0x11); shift = IsPhysicallyDown(0x10); alt = IsPhysicallyDown(0x12);
            if (!IsPhysicallyDown((int)favoriteHotkey.VirtualKey)) handledF = false;
            if (!IsPhysicallyDown((int)unfavoriteHotkey.VirtualKey)) handledU = false;
            if (!IsPhysicallyDown((int)openHotkey.VirtualKey)) handledP = false;
        }

        private static bool IsPhysicallyDown(int key) { return (NativeMethods.GetAsyncKeyState(key) & 0x8000) != 0; }

        private void MessageLoop()
        {
            nativeThreadId = NativeMethods.GetCurrentThreadId();
            hook = NativeMethods.SetWindowsHookEx(13, callback, IntPtr.Zero, 0);
            if (hook == IntPtr.Zero) { Log.Write("HOOK_FAILED", Marshal.GetLastWin32Error().ToString()); return; }
            Log.Write("HOOK_INSTALLED", "dedicated message-loop thread");
            NativeMethods.Msg msg;
            while (NativeMethods.GetMessage(out msg, IntPtr.Zero, 0, 0) > 0) { NativeMethods.TranslateMessage(ref msg); NativeMethods.DispatchMessage(ref msg); }
            NativeMethods.UnhookWindowsHookEx(hook); hook = IntPtr.Zero;
            Log.Write("HOOK_UNINSTALLED", "keyboard hook removed");
        }

        private IntPtr Hook(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code < 0) return NativeMethods.CallNextHookEx(hook, code, wParam, lParam);
            int message = wParam.ToInt32();
            bool down = message == 0x100 || message == 0x104;
            bool up = message == 0x101 || message == 0x105;
            NativeMethods.KeyboardData data = (NativeMethods.KeyboardData)Marshal.PtrToStructure(lParam, typeof(NativeMethods.KeyboardData));
            if ((data.Flags & 0x10) != 0)
            {
                if (IsConfiguredKey(data.Vk))
                    EnqueueDiagnostic(data.Vk, down, up, NativeMethods.GetForegroundWindow(), false, "injected input ignored");
                return NativeMethods.CallNextHookEx(hook, code, wParam, lParam);
            }
            if (IsModifier(data.Vk))
            {
                if (down) SetModifier(data.Vk, true); else if (up) SetModifier(data.Vk, false);
                return NativeMethods.CallNextHookEx(hook, code, wParam, lParam);
            }
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            // The worker owns process classification. If foreground changed before its
            // next reconciliation tick, fail closed instead of classifying in the hook.
            if (hwnd == IntPtr.Zero || hwnd != foregroundHwnd)
            {
                if (data.Vk == 0x46 || data.Vk == 0x50 || data.Vk == 0x55) EnqueueDiagnostic(data.Vk, down, up, hwnd, false, hwnd == IntPtr.Zero ? "HWND zero" : "foreground cache mismatch");
                return NativeMethods.CallNextHookEx(hook, code, wParam, lParam);
            }
            ForegroundKind kind = hwnd == IntPtr.Zero ? ForegroundKind.Other : foregroundKind;
            if (!accepting) return NativeMethods.CallNextHookEx(hook, code, wParam, lParam);
            if (Matches(data.Vk, kind, ForegroundKind.Explorer, openHotkey))
            {
                if (down) { if (!handledP) { handledP = true; enqueue(new InputAction(InputActionKind.ExplorerOpen, hwnd)); EnqueueDiagnostic(data.Vk, down, up, hwnd, true, "Explorer open queued"); } else EnqueueDiagnostic(data.Vk, down, up, hwnd, true, "Explorer open repeat suppressed"); return new IntPtr(1); }
                if (up && handledP) { handledP = false; EnqueueDiagnostic(data.Vk, down, up, hwnd, true, "Explorer open key-up suppressed"); return new IntPtr(1); }
            }
            if (Matches(data.Vk, kind, ForegroundKind.Photos, favoriteHotkey))
            {
                if (down) { if (!handledF) { handledF = true; enqueue(new InputAction(InputActionKind.Favorite, hwnd) { ObservationVersion = Interlocked.Read(ref observationVersion) }); EnqueueDiagnostic(data.Vk, down, up, hwnd, true, "Photos favorite queued"); } else EnqueueDiagnostic(data.Vk, down, up, hwnd, true, "Photos favorite repeat suppressed"); return new IntPtr(1); }
                if (up && handledF) { handledF = false; return new IntPtr(1); }
            }
            if (Matches(data.Vk, kind, ForegroundKind.Photos, unfavoriteHotkey))
            {
                if (down) { if (!handledU) { handledU = true; enqueue(new InputAction(InputActionKind.Unfavorite, hwnd) { ObservationVersion = Interlocked.Read(ref observationVersion) }); EnqueueDiagnostic(data.Vk, down, up, hwnd, true, "Photos unfavorite queued"); } else EnqueueDiagnostic(data.Vk, down, up, hwnd, true, "Photos unfavorite repeat suppressed"); return new IntPtr(1); }
                if (up && handledU) { handledU = false; return new IntPtr(1); }
            }
            if (IsConfiguredKey(data.Vk)) EnqueueDiagnostic(data.Vk, down, up, hwnd, false, "configured chord did not match");
            return NativeMethods.CallNextHookEx(hook, code, wParam, lParam);
        }

        private void EnqueueDiagnostic(uint vk, bool down, bool up, IntPtr actualHwnd, bool suppressed, string decision)
        {
            enqueue(new InputAction(InputActionKind.HookDiagnostic, actualHwnd)
            {
                Vk = vk, Down = down, Up = up, Ctrl = ctrl, Shift = shift, Alt = alt,
                Suppressed = suppressed, CachedHwnd = foregroundHwnd, CachedKind = foregroundKind, Decision = decision
            });
        }

        private static bool IsModifier(uint key) { return key == 0x10 || key == 0xA0 || key == 0xA1 || key == 0x11 || key == 0xA2 || key == 0xA3 || key == 0x12 || key == 0xA4 || key == 0xA5; }
        private bool Matches(uint key, ForegroundKind actual, ForegroundKind required, Hotkey hotkey) { return actual == required && key == hotkey.VirtualKey && ctrl == hotkey.Ctrl && shift == hotkey.Shift && !alt; }
        private bool IsConfiguredKey(uint key) { return key == openHotkey.VirtualKey || key == favoriteHotkey.VirtualKey || key == unfavoriteHotkey.VirtualKey; }
        private void SetModifier(uint key, bool down) { if (key == 0x10 || key == 0xA0 || key == 0xA1) shift = down; else if (key == 0x11 || key == 0xA2 || key == 0xA3) ctrl = down; else alt = down; }

        public void Dispose()
        {
            accepting = false;
            if (nativeThreadId != 0) NativeMethods.PostThreadMessage(nativeThreadId, 0x0012, IntPtr.Zero, IntPtr.Zero);
            if (thread.IsAlive) thread.Join(2000);
        }
    }
}
