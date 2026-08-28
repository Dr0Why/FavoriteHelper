using System;
using System.Windows.Automation;

namespace FavoriteHelper
{
    internal sealed class PhotosReader
    {
        public string ReadBasename(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || WindowClassifier.PhotosPid(hwnd) == 0) return String.Empty;
            try
            {
                AutomationElement root = AutomationElement.FromHandle(hwnd);
                AutomationElement title = root.FindFirst(TreeScope.Subtree, new PropertyCondition(AutomationElement.AutomationIdProperty, "TitleBarTitle"));
                return title == null ? String.Empty : (title.Current.Name ?? String.Empty);
            }
            catch { return String.Empty; }
        }
    }
}
