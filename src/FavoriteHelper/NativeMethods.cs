using System;
using System.Runtime.InteropServices;

namespace FavoriteHelper
{
    internal static class NativeMethods
    {
        internal delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
        internal delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr data);

        [StructLayout(LayoutKind.Sequential)] internal struct KeyboardData { public uint Vk, Scan, Flags, Time; public IntPtr ExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] internal struct Msg { public IntPtr Hwnd; public uint Message; public IntPtr WParam, LParam; public uint Time; public Point Point; }

        [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint threadId);
        [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll")] internal static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr data);
        [DllImport("user32.dll")] internal static extern sbyte GetMessage(out Msg msg, IntPtr hwnd, uint min, uint max);
        [DllImport("user32.dll")] internal static extern bool TranslateMessage(ref Msg msg);
        [DllImport("user32.dll")] internal static extern IntPtr DispatchMessage(ref Msg msg);
        [DllImport("user32.dll")] internal static extern bool PostThreadMessage(uint threadId, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] internal static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    }
}
