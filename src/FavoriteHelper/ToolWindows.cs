using System;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace FavoriteHelper
{
    internal abstract class DropBatchWindow : Form
    {
        private readonly ListBox selection = new ListBox();
        private readonly TextBox results = new TextBox();
        private readonly Button execute = new Button();
        private readonly string action;
        protected DropBatchWindow(string action)
        {
            this.action = action; Text = "FavoriteHelper " + action; StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(700, 480); MinimumSize = new Size(520, 360); AllowDrop = true;
            Label prompt = new Label { Text = "Drop one or more .lnk files here. Dropping does not start " + action + ".", AutoSize = true, Location = new Point(12, 14) };
            selection.Location = new Point(12, 40); selection.Size = new Size(660, 150); selection.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            execute.Text = action; execute.Location = new Point(12, 200); execute.Size = new Size(100, 30); execute.Click += delegate { StartBatch(); };
            results.Location = new Point(12, 240); results.Size = new Size(660, 190); results.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right; results.Multiline = true; results.ReadOnly = true; results.ScrollBars = ScrollBars.Both; results.WordWrap = false;
            Controls.Add(prompt); Controls.Add(selection); Controls.Add(execute); Controls.Add(results);
            DragEnter += OnDragEnter; DragDrop += OnDragDrop;
        }
        private void OnDragEnter(object sender, DragEventArgs e) { e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; }
        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[]; if (paths == null) return;
            selection.Items.Clear(); foreach (string path in paths) selection.Items.Add(path);
            results.Text = paths.Length + " explicitly selected item(s). Click " + action + " to begin.";
        }
        private void StartBatch()
        {
            if (selection.Items.Count == 0) { results.Text = "Drop at least one path first."; return; }
            string[] batch = new string[selection.Items.Count]; selection.Items.CopyTo(batch, 0);
            execute.Enabled = false; AllowDrop = false; results.Text = action + " is running...";
            ThreadPool.QueueUserWorkItem(delegate
            {
                string report; try { report = ExecuteBatch(batch); } catch (Exception ex) { report = action + " failed: " + ex.Message; }
                try { BeginInvoke(new Action(delegate { if (!IsDisposed) { results.Text = report; execute.Enabled = true; AllowDrop = true; } })); } catch { }
            });
        }
        protected abstract string ExecuteBatch(string[] paths);
    }

    internal sealed class ExportWindow : DropBatchWindow
    {
        private readonly Func<string> folderName;
        public ExportWindow(Func<string> folderName) : base("Export") { this.folderName = folderName; }
        protected override string ExecuteBatch(string[] paths)
        {
            string frozenFolderName = folderName();
            ExportBatchResult batch = new ExportService(new WindowsFileValidator(), new ShellShortcutStore(), delegate { return frozenFolderName; }).Export(paths);
            StringBuilder text = new StringBuilder(); text.AppendLine("Exported: " + batch.ExportedCount).AppendLine("Skipped existing: " + batch.SkippedAlreadyExistsCount).AppendLine("Skipped/rejected: " + batch.RejectedCount).AppendLine("Failed: " + batch.FailedCount).AppendLine();
            foreach (ExportItemResult item in batch.Items) text.AppendLine(item.Status + " | " + item.ShortcutPath + " | " + item.Reason);
            return text.ToString();
        }
    }

    internal sealed class RepairWindow : DropBatchWindow
    {
        private readonly Func<string> folderName;
        public RepairWindow(Func<string> folderName) : base("Repair") { this.folderName = folderName; }
        protected override string ExecuteBatch(string[] paths)
        {
            string frozenFolderName = folderName();
            ShortcutMigrationService service = new ShortcutMigrationService(new WindowsFileValidator(), new ShellShortcutStore(), delegate { return frozenFolderName; });
            int repaired = 0, current = 0, skipped = 0, failed = 0; StringBuilder details = new StringBuilder();
            foreach (string path in paths)
            {
                ShortcutMigrationResult result; try { result = service.Migrate(path); } catch (Exception ex) { result = new ShortcutMigrationResult(ShortcutMigrationStatus.Failed, ex.Message); }
                if (result.Status == ShortcutMigrationStatus.Migrated) repaired++; else if (result.Status == ShortcutMigrationStatus.AlreadyCurrent) current++; else if (result.Status == ShortcutMigrationStatus.Refused) skipped++; else failed++;
                details.AppendLine(result.Status + " | " + path + " | " + result.Message);
            }
            return "Repaired: " + repaired + "\r\nAlready current: " + current + "\r\nSkipped/rejected: " + skipped + "\r\nFailed: " + failed + "\r\n\r\n" + details;
        }
    }

    internal sealed class ConfigurationWindow : Form
    {
        private readonly TextBox value = new TextBox(); private readonly Label error = new Label(); private readonly Func<string, string> save;
        public ConfigurationWindow(Func<string> current, Func<string, string> save)
        {
            this.save = save; Text = "FavoriteHelper Configuration"; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(430, 145); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            Controls.Add(new Label { Text = "Favorite folder name", AutoSize = true, Location = new Point(12, 14) }); value.Location = new Point(12, 38); value.Width = 400; value.Text = current(); Controls.Add(value);
            Button saveButton = new Button { Text = "Save", Location = new Point(236, 72), Size = new Size(82, 28) }; saveButton.Click += SaveValue; Controls.Add(saveButton);
            Button cancel = new Button { Text = "Cancel", Location = new Point(330, 72), Size = new Size(82, 28), DialogResult = DialogResult.Cancel }; cancel.Click += delegate { Close(); }; Controls.Add(cancel);
            error.Location = new Point(12, 108); error.Size = new Size(400, 32); error.ForeColor = Color.DarkRed; Controls.Add(error); CancelButton = cancel; AcceptButton = saveButton;
        }
        private void SaveValue(object sender, EventArgs e) { string message = save(value.Text); if (message == null) Close(); else error.Text = message; }
    }
}
