using System;
using System.Diagnostics;

namespace FavoriteHelper
{
    internal enum ForegroundKind { Other, Explorer, Photos }

    internal static class WindowClassifier
    {
        internal static bool IsPhotosProcessName(string processName)
        {
            return String.Equals(processName, "PhotosApp", StringComparison.OrdinalIgnoreCase)
                || String.Equals(processName, "Photos", StringComparison.OrdinalIgnoreCase);
        }

        internal static string ProcessName(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return String.Empty;
            uint pid;
            NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
            if (pid == 0) return String.Empty;
            try { return Process.GetProcessById((int)pid).ProcessName; }
            catch { return String.Empty; }
        }

        internal static uint PhotosPid(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return 0;
            uint direct;
            NativeMethods.GetWindowThreadProcessId(hwnd, out direct);
            if (IsPhotosProcessName(ProcessName(hwnd))) return direct;
            uint found = 0;
            NativeMethods.EnumChildWindows(hwnd, delegate(IntPtr child, IntPtr data)
            {
                uint pid;
                NativeMethods.GetWindowThreadProcessId(child, out pid);
                if (IsPhotosProcessName(ProcessName(child))) { found = pid; return false; }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        internal static ForegroundKind Classify(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return ForegroundKind.Other;
            if (String.Equals(ProcessName(hwnd), "explorer", StringComparison.OrdinalIgnoreCase)) return ForegroundKind.Explorer;
            return PhotosPid(hwnd) == 0 ? ForegroundKind.Other : ForegroundKind.Photos;
        }

        internal static bool IsProcessAlive(uint pid)
        {
            if (pid == 0) return false;
            try { return !Process.GetProcessById((int)pid).HasExited; }
            catch { return false; }
        }
    }
}
