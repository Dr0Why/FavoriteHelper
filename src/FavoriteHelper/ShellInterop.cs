using System;
using System.Runtime.InteropServices;

namespace FavoriteHelper
{
    [ComImport, Guid("6d5140c1-7436-11ce-8034-00aa006009fa"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IComServiceProvider { [PreserveSig] int QueryService(ref Guid service, ref Guid iid, out IntPtr obj); }

    [ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellBrowser
    {
        [PreserveSig] int GetWindow(out IntPtr hwnd); [PreserveSig] int ContextSensitiveHelp(bool enter); [PreserveSig] int InsertMenusSB(IntPtr menu, IntPtr widths); [PreserveSig] int SetMenuSB(IntPtr menu, IntPtr hole, IntPtr active); [PreserveSig] int RemoveMenusSB(IntPtr menu); [PreserveSig] int SetStatusTextSB(IntPtr text); [PreserveSig] int EnableModelessSB(bool enable); [PreserveSig] int TranslateAcceleratorSB(IntPtr msg, ushort id); [PreserveSig] int BrowseObject(IntPtr pidl, uint flags); [PreserveSig] int GetViewStateStream(uint mode, out IntPtr stream); [PreserveSig] int GetControlWindow(uint id, out IntPtr hwnd); [PreserveSig] int SendControlMsg(uint id, uint msg, IntPtr wp, IntPtr lp, out IntPtr result); [PreserveSig] int QueryActiveShellView(out IntPtr view); [PreserveSig] int OnViewWindowActive(IntPtr view); [PreserveSig] int SetToolbarItems(IntPtr buttons, uint count, uint flags);
    }

    [ComImport, Guid("1AF3A467-214F-4298-908E-06B03E0B39F9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IFolderView2
    {
        [PreserveSig] int GetCurrentViewMode(out uint mode); [PreserveSig] int SetCurrentViewMode(uint mode); [PreserveSig] int GetFolder(ref Guid riid, out IntPtr folder); [PreserveSig] int Item(int index, out IntPtr pidl); [PreserveSig] int ItemCount(uint flags, out int count); [PreserveSig] int Items(uint flags, ref Guid riid, out IntPtr items); [PreserveSig] int GetSelectionMarkedItem(out int index); [PreserveSig] int GetFocusedItem(out int index); [PreserveSig] int GetItemPosition(IntPtr pidl, out NativeMethods.Point pt); [PreserveSig] int GetSpacing(out NativeMethods.Point pt); [PreserveSig] int GetDefaultSpacing(out NativeMethods.Point pt); [PreserveSig] int GetAutoArrange(); [PreserveSig] int SelectItem(int item, uint flags); [PreserveSig] int SelectAndPositionItems(uint count, IntPtr pidls, IntPtr points, uint flags); [PreserveSig] int SetGroupBy(IntPtr key, bool ascending); [PreserveSig] int GetGroupBy(IntPtr key, out bool ascending); [PreserveSig] int SetViewProperty(IntPtr pidl, IntPtr key, IntPtr value); [PreserveSig] int GetViewProperty(IntPtr pidl, IntPtr key, out IntPtr value); [PreserveSig] int SetTileViewProperties(IntPtr pidl, string props); [PreserveSig] int SetExtendedTileViewProperties(IntPtr pidl, string props); [PreserveSig] int SetText(uint type, string text); [PreserveSig] int SetCurrentFolderFlags(uint mask, uint flags); [PreserveSig] int GetCurrentFolderFlags(out uint flags); [PreserveSig] int GetSortColumnCount(out int count); [PreserveSig] int SetSortColumns(IntPtr columns, int count); [PreserveSig] int GetSortColumns(IntPtr columns, int count); [PreserveSig] int GetItem(int index, ref Guid riid, out IntPtr item); [PreserveSig] int GetVisibleItem(int start, bool previous, out int item); [PreserveSig] int GetSelectedItem(int start, out int item); [PreserveSig] int GetSelection(bool noneImpliesFolder, out IntPtr array); [PreserveSig] int GetSelectionState(IntPtr pidl, out uint flags); [PreserveSig] int InvokeVerbOnSelection([MarshalAs(UnmanagedType.LPWStr)] string verb);
    }
}
