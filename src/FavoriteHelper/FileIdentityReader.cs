using System;
using System.Runtime.InteropServices;

namespace FavoriteHelper
{
    internal interface IFileValidator
    {
        FileIdentity Read(string path);
    }

    internal sealed class WindowsFileValidator : IFileValidator
    {
        private static readonly IntPtr InvalidHandle = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)] private struct FileTime { public uint Low, High; }
        [StructLayout(LayoutKind.Sequential)] private struct ByHandleFileInformation
        {
            public uint Attributes;
            public FileTime Creation, Access, Write;
            public uint VolumeSerial, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetFileInformationByHandle(IntPtr handle, out ByHandleFileInformation info);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

        public FileIdentity Read(string path)
        {
            IntPtr handle = CreateFile(path, 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
            if (handle == InvalidHandle) return null;
            try
            {
                ByHandleFileInformation info;
                if (!GetFileInformationByHandle(handle, out info)) return null;
                ulong index = ((ulong)info.IndexHigh << 32) | info.IndexLow;
                return new FileIdentity(info.VolumeSerial, index);
            }
            finally { CloseHandle(handle); }
        }
    }
}
