using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace FavoriteHelper
{
    internal sealed class TrayContext : ApplicationContext
    {
        private readonly NotifyIcon icon;
        private readonly ToolStripMenuItem exitItem;
        private readonly Action requestExit;
        private readonly SynchronizationContext ui;
        private NotificationPopup popup;
        private ExportWindow exportWindow;
        private RepairWindow repairWindow;
        private ConfigurationWindow configurationWindow;
        private bool exiting;

        public TrayContext(Action requestExit, Func<string> folderName, Func<string, string> saveFolderName)
        {
            this.requestExit = requestExit;
            ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            exitItem = new ToolStripMenuItem("Exit", null, delegate { BeginExit(); });
            ToolStripMenuItem exportItem = new ToolStripMenuItem("Export...", null, delegate { ShowExport(folderName); });
            ToolStripMenuItem repairItem = new ToolStripMenuItem("Repair...", null, delegate { ShowRepair(folderName); });
            ToolStripMenuItem configItem = new ToolStripMenuItem("Configuration...", null, delegate { ShowConfiguration(folderName, saveFolderName); });
            ContextMenuStrip menu = new ContextMenuStrip { Font = SystemFonts.MenuFont }; menu.Items.Add(exportItem); menu.Items.Add(repairItem); menu.Items.Add(configItem); menu.Items.Add(new ToolStripSeparator()); menu.Items.Add(exitItem);
            icon = new NotifyIcon { Text = "FavoriteHelper", Icon = AppIcon.Load(), ContextMenuStrip = menu, Visible = true };
        }

        private void ShowExport(Func<string> folderName) { if (exportWindow == null || exportWindow.IsDisposed) { exportWindow = new ExportWindow(folderName); exportWindow.FormClosed += delegate { exportWindow = null; }; } ShowWindow(exportWindow); }
        private void ShowRepair(Func<string> folderName) { if (repairWindow == null || repairWindow.IsDisposed) { repairWindow = new RepairWindow(folderName); repairWindow.FormClosed += delegate { repairWindow = null; }; } ShowWindow(repairWindow); }
        private void ShowConfiguration(Func<string> folderName, Func<string, string> saveFolderName) { if (configurationWindow == null || configurationWindow.IsDisposed) { configurationWindow = new ConfigurationWindow(folderName, saveFolderName); configurationWindow.FormClosed += delegate { configurationWindow = null; }; } ShowWindow(configurationWindow); }
        private static void ShowWindow(Form form) { if (!form.Visible) form.Show(); if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal; form.Activate(); }

        private void BeginExit()
        {
            if (exiting) return;
            exiting = true; exitItem.Enabled = false;
            if (exportWindow != null) exportWindow.Close();
            if (repairWindow != null) repairWindow.Close();
            if (configurationWindow != null) configurationWindow.Close();
            requestExit();
        }

        public void Notify(string message, bool safety, bool routineEnabled)
        {
            if (!NotificationPolicy.ShouldShow(safety, routineEnabled)) return;
            ui.Post(delegate
            {
                if (exiting) return;
                if (popup != null) popup.Close();
                popup = new NotificationPopup(message, safety, AppIcon.Load());
                popup.FormClosed += delegate { popup = null; };
                popup.ShowWithoutFocus();
            }, null);
        }

        public void CompleteExit()
        {
            ui.Post(delegate { if (popup != null) popup.Close(); icon.Visible = false; icon.Dispose(); ExitThread(); }, null);
        }

        protected override void Dispose(bool disposing) { if (disposing) { icon.Visible = false; icon.Dispose(); } base.Dispose(disposing); }
    }

    internal static class AppIcon
    {
        public static Icon Load()
        {
            try
            {
                string local = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FavoriteHelper.ico");
                if (System.IO.File.Exists(local)) return new Icon(local);
                Icon associated = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (associated != null) return (Icon)associated.Clone();
            }
            catch { }
            return (Icon)SystemIcons.Application.Clone();
        }
    }

    internal sealed class NotificationPopup : Form
    {
        private const int WsExToolWindow = 0x00000080;
        private const int WsExNoActivate = 0x08000000;
        private const int MinimumTextWidth = 250;
        private const int MaximumTextWidth = 420;
        private readonly System.Windows.Forms.Timer lifetime;

        public NotificationPopup(string message, bool safety, Icon applicationIcon)
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = safety ? Color.FromArgb(255, 244, 230) : Color.White;
            Padding = new Padding(12);

            string titleText = safety ? "FavoriteHelper — Safety" : "FavoriteHelper";
            Font titleFont = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold);
            Font bodyFont = SystemFonts.MessageBoxFont;
            TextFormatFlags singleLine = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            Size titleSize = TextRenderer.MeasureText(titleText, titleFont, Size.Empty, singleLine);
            Size messageLine = TextRenderer.MeasureText(message ?? String.Empty, bodyFont, Size.Empty, singleLine);
            int textWidth = Math.Max(MinimumTextWidth, Math.Max(titleSize.Width, messageLine.Width));
            textWidth = Math.Min(MaximumTextWidth, textWidth);
            Size bodySize = TextRenderer.MeasureText(message ?? String.Empty, bodyFont, new Size(textWidth, 10000), TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
            bodySize.Height = Math.Max(bodyFont.Height, bodySize.Height);

            const int textLeft = 72, top = 12, titleBodyGap = 8, bottom = 12;
            int bodyTop = top + titleSize.Height + titleBodyGap;
            ClientSize = new Size(textLeft + textWidth + 18, Math.Max(72, bodyTop + bodySize.Height + bottom));
            PictureBox picture = new PictureBox { Image = applicationIcon.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Location = new Point(12, (ClientSize.Height - 48) / 2), Size = new Size(48, 48), TabStop = false };
            Label title = new Label { AutoSize = false, Text = titleText, Font = titleFont, Location = new Point(textLeft, top), Size = new Size(textWidth, titleSize.Height) };
            Label body = new Label { AutoSize = false, Text = message, Font = bodyFont, Location = new Point(textLeft, bodyTop), Size = new Size(textWidth, bodySize.Height) };
            Controls.Add(picture); Controls.Add(title); Controls.Add(body);
            lifetime = new System.Windows.Forms.Timer { Interval = safety ? 6000 : 4000 };
            lifetime.Tick += delegate { lifetime.Stop(); Close(); };
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { CreateParams value = base.CreateParams; value.ExStyle |= WsExToolWindow | WsExNoActivate; return value; } }

        public void ShowWithoutFocus()
        {
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Right - Width - 12, area.Bottom - Height - 12);
            Show(); lifetime.Start();
        }

        protected override void Dispose(bool disposing) { if (disposing) lifetime.Dispose(); base.Dispose(disposing); }
    }
}
