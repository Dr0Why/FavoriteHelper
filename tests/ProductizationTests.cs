using System;
using System.IO;
using System.Threading;
using FavoriteHelper;
internal static class ProductizationTests
{
    private static int failures;
    private static void Check(bool condition, string name) { Console.WriteLine((condition ? "PASS " : "FAIL ") + name); if (!condition) failures++; }
    public static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "FavoriteHelper-product-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "config.json"), warning; AppConfig defaults = AppConfig.Load(path, out warning);
            Check(defaults.Open.Text == "Ctrl+Shift+P" && defaults.Favorite.Text == "Ctrl+F" && defaults.Unfavorite.Text == "Ctrl+Shift+U", "default P/F/U hotkeys");
            Check(File.Exists(path) && warning == null, "missing config created app-locally");
            File.WriteAllText(path, "{bad json"); AppConfig malformed = AppConfig.Load(path, out warning);
            Check(warning != null && malformed.Open.Text == "Ctrl+Shift+P", "malformed config fails to safe defaults");
            File.WriteAllText(path, "{\"open_hotkey\":\"P\",\"favorite_hotkey\":\"Ctrl+F\",\"unfavorite_hotkey\":\"Ctrl+Shift+U\",\"enable_notification\":false}");
            AppConfig unsafeValue = AppConfig.Load(path, out warning); Check(warning != null && unsafeValue.EnableNotification, "unsafe hotkey rejected as a unit");
            File.WriteAllText(path, "{\"open_hotkey\":\"Ctrl+Shift+P\",\"favorite_hotkey\":\"Ctrl+F\",\"unfavorite_hotkey\":\"Ctrl+Shift+U\",\"enable_notification\":false}");
            AppConfig quiet = AppConfig.Load(path, out warning); Check(warning == null && !quiet.EnableNotification, "valid notification suppression is honored");
            Check(!NotificationPolicy.ShouldShow(false, false), "quiet config suppresses routine notifications");
            Check(NotificationPolicy.ShouldShow(false, true), "enabled config shows routine notifications");
            Check(NotificationPolicy.ShouldShow(true, false), "quiet config cannot suppress safety notifications");
            FavoriteResult favoriteSuccess = new FavoriteResult(true, FavoriteState.Favorited, "favorite created");
            FavoriteResult unfavoriteSuccess = new FavoriteResult(true, FavoriteState.NotFavorited, "favorite removed");
            FavoriteResult broken = new FavoriteResult(false, FavoriteState.Broken, "unsafe shortcut");
            Check(NotificationPolicy.ShouldShow(favoriteSuccess, true) && NotificationPolicy.ShouldShow(unfavoriteSuccess, true), "Favorite and Unfavorite success are routine-visible");
            Check(!NotificationPolicy.ShouldShow(favoriteSuccess, false) && !NotificationPolicy.ShouldShow(unfavoriteSuccess, false), "quiet config suppresses Favorite and Unfavorite success");
            Check(NotificationPolicy.ShouldShow(broken, false) && NotificationPolicy.IsSafety(broken), "safety result remains visible in quiet mode");
            FavoriteOperationQueue queue = new FavoriteOperationQueue(null); queue.StopAccepting(); Check(!queue.Enqueue(null) && queue.Count == 0, "exit gate prevents new filesystem queue entries");
            string image = Path.Combine(root, "controlled-exit.jpg"); File.WriteAllBytes(image, new byte[] { 1, 2, 3 });
            WindowsFileValidator files = new WindowsFileValidator(); BlockingStore store = new BlockingStore(); FavoriteService service = new FavoriteService(files, store);
            SourceItem item = new SourceItem(image, Path.GetFileName(image), files.Read(image)); FavoriteOperationQueue active = new FavoriteOperationQueue(service);
            active.Enqueue(new FavoriteOperationRequest(FavoriteAction.Favorite, item)); FavoriteResult result = null;
            Thread mutation = new Thread(new ThreadStart(delegate { result = active.ExecuteNext(); })); mutation.Start(); Check(store.Entered.WaitOne(5000), "controlled exit reaches active mutation");
            active.StopAccepting(); Check(!active.Enqueue(new FavoriteOperationRequest(FavoriteAction.Unfavorite, item)), "controlled exit rejects later mutation");
            store.Release.Set(); Check(mutation.Join(10000), "active mutation reaches safe completion point");
            string favoriteDirectory = service.DirectoryPath(item); string[] temporary = Directory.Exists(favoriteDirectory) ? Directory.GetFiles(favoriteDirectory, ".favoritehelper-*.tmp.lnk") : new string[0];
            Check(result != null && result.Changed && temporary.Length == 0, "controlled exit leaves final link or cleanup, never partial final link");
        }
        finally { Directory.Delete(root, true); }
        return failures == 0 ? 0 : 1;
    }

    private sealed class BlockingStore : IShortcutStore
    {
        internal readonly ManualResetEvent Entered = new ManualResetEvent(false), Release = new ManualResetEvent(false);
        private readonly ShellShortcutStore inner = new ShellShortcutStore();
        public void Create(string target, string shortcut) { Entered.Set(); Release.WaitOne(); inner.Create(target, shortcut); }
        public string ReadRelativePath(string shortcut) { return inner.ReadRelativePath(shortcut); }
    }
}
