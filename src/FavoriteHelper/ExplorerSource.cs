using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace FavoriteHelper
{
    internal sealed class ExplorerSource
    {
        private readonly IFileValidator files;
        private readonly HashSet<string> extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".heic" };
        public ExplorerSource(IFileValidator files) { this.files = files; }

        private dynamic FindWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || WindowClassifier.Classify(hwnd) != ForegroundKind.Explorer) return null;
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
            foreach (dynamic window in shell.Windows())
            {
                try { if (new IntPtr((long)window.HWND) == hwnd) return window; } catch { }
            }
            return null;
        }

        public bool TryCaptureAndOpen(IntPtr hwnd, out SourceItem selected, out SourceSnapshot snapshot, out string error)
        {
            selected = null; snapshot = null; error = null;
            try
            {
                dynamic window = FindWindow(hwnd);
                if (window == null) { error = "unsupported or virtual Explorer window"; return false; }
                dynamic document = window.Document;
                dynamic selection = document.SelectedItems();
                if ((int)selection.Count != 1) { error = "selection count is not one"; return false; }
                string selectedPath = (string)selection.Item(0).Path;
                if (!IsImagePath(selectedPath)) { error = "selection is not a supported local image"; return false; }

                List<SourceItem> items = new List<SourceItem>();
                dynamic viewItems = document.Folder.Items();
                for (int i = 0; i < (int)viewItems.Count; i++)
                {
                    string path = null;
                    try { path = (string)viewItems.Item(i).Path; } catch { }
                    if (!IsImagePath(path)) continue;
                    FileIdentity identity = files.Read(path);
                    if (identity == null) { error = "file identity unavailable for snapshot item"; return false; }
                    items.Add(new SourceItem(path, Path.GetFileName(path), identity));
                }
                SourceItem selectedMatch = null;
                foreach (SourceItem item in items)
                    if (String.Equals(item.FullPath, selectedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (selectedMatch != null) { error = "selected path is ambiguous in Shell View"; return false; }
                        selectedMatch = item;
                    }
                if (selectedMatch == null) { error = "selection absent from Shell View snapshot"; return false; }
                if (!InvokeDefaultVerb(window, out error)) return false;
                selected = selectedMatch;
                snapshot = new SourceSnapshot(items);
                return true;
            }
            catch (Exception ex) { error = ex.GetType().Name + ": " + ex.Message; return false; }
        }

        private bool IsImagePath(string path)
        {
            return !String.IsNullOrEmpty(path) && Path.IsPathRooted(path) && File.Exists(path) && extensions.Contains(Path.GetExtension(path));
        }

        private static bool InvokeDefaultVerb(dynamic window, out string error)
        {
            error = null;
            IntPtr browserPtr = IntPtr.Zero, viewPtr = IntPtr.Zero, folderViewPtr = IntPtr.Zero;
            try
            {
                IComServiceProvider provider = (IComServiceProvider)window;
                Guid service = new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837");
                Guid browserIid = typeof(IShellBrowser).GUID;
                int hr = provider.QueryService(ref service, ref browserIid, out browserPtr);
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);
                IShellBrowser browser = (IShellBrowser)Marshal.GetObjectForIUnknown(browserPtr);
                hr = browser.QueryActiveShellView(out viewPtr);
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);
                Guid folderViewIid = typeof(IFolderView2).GUID;
                hr = Marshal.QueryInterface(viewPtr, ref folderViewIid, out folderViewPtr);
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);
                IFolderView2 folderView = (IFolderView2)Marshal.GetObjectForIUnknown(folderViewPtr);
                hr = folderView.InvokeVerbOnSelection(null);
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);
                return true;
            }
            catch (Exception ex) { error = "InvokeVerbOnSelection failed: " + ex.Message; return false; }
            finally
            {
                if (folderViewPtr != IntPtr.Zero) Marshal.Release(folderViewPtr);
                if (viewPtr != IntPtr.Zero) Marshal.Release(viewPtr);
                if (browserPtr != IntPtr.Zero) Marshal.Release(browserPtr);
            }
        }
    }
}
