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
        private bool exiting;

        public TrayContext(Action requestExit)
        {
            this.requestExit = requestExit;
            ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            exitItem = new ToolStripMenuItem("Exit", null, delegate { BeginExit(); });
            ContextMenuStrip menu = new ContextMenuStrip(); menu.Items.Add(exitItem);
            icon = new NotifyIcon { Text = "FavoriteHelper", Icon = AppIcon.Load(), ContextMenuStrip = menu, Visible = true };
        }

        private void BeginExit()
        {
            if (exiting) return;
            exiting = true; exitItem.Enabled = false;
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
        private readonly System.Windows.Forms.Timer lifetime;

        public NotificationPopup(string message, bool safety, Icon applicationIcon)
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(340, 86);
            BackColor = safety ? Color.FromArgb(255, 244, 230) : Color.White;
            Padding = new Padding(12);

            PictureBox picture = new PictureBox { Image = applicationIcon.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Location = new Point(12, 19), Size = new Size(48, 48), TabStop = false };
            Label title = new Label { AutoSize = false, Text = safety ? "FavoriteHelper — Safety" : "FavoriteHelper", Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold), Location = new Point(72, 13), Size = new Size(250, 22) };
            Label body = new Label { AutoEllipsis = true, AutoSize = false, Text = message, Font = SystemFonts.MessageBoxFont, Location = new Point(72, 37), Size = new Size(250, 38) };
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
