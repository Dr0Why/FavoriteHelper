using System;
using System.Runtime.InteropServices;
using System.Text;

namespace FavoriteHelper
{
    [ComImport, Guid("00021401-0000-0000-C000-000000000046")] internal class ShellLinkObject { }
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    internal interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int max, IntPtr data, uint flags); void GetIDList(out IntPtr pidl); void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int max); void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int max); void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int max); void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out short key); void SetHotkey(short key); void GetShowCmd(out int cmd); void SetShowCmd(int cmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder icon, int max, out int index); void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string icon, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved); void Resolve(IntPtr hwnd, uint flags); void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0000010b-0000-0000-C000-000000000046")]
    internal interface IPersistFile { void GetClassID(out Guid id); void IsDirty(); void Load([MarshalAs(UnmanagedType.LPWStr)] string file, uint mode); void Save([MarshalAs(UnmanagedType.LPWStr)] string file, bool remember); void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string file); void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string file); }
}
