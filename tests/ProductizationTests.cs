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
            Check(defaults.FavoriteFolderName == "Favorite", "missing favorite_folder_name defaults to Favorite");
            Check(File.Exists(path) && warning == null, "missing config created app-locally");
            File.WriteAllText(path, "{bad json"); AppConfig malformed = AppConfig.Load(path, out warning);
            Check(warning != null && malformed.Open.Text == "Ctrl+Shift+P", "malformed config fails to safe defaults");
            File.WriteAllText(path, "{\"open_hotkey\":\"P\",\"favorite_hotkey\":\"Ctrl+F\",\"unfavorite_hotkey\":\"Ctrl+Shift+U\",\"enable_notification\":false}");
            AppConfig unsafeValue = AppConfig.Load(path, out warning); Check(warning != null && unsafeValue.EnableNotification, "unsafe hotkey rejected as a unit");
            File.WriteAllText(path, "{\"open_hotkey\":\"Ctrl+Shift+P\",\"favorite_hotkey\":\"Ctrl+F\",\"unfavorite_hotkey\":\"Ctrl+Shift+U\",\"enable_notification\":false}");
            AppConfig quiet = AppConfig.Load(path, out warning); Check(warning == null && !quiet.EnableNotification, "valid notification suppression is honored");
            File.WriteAllText(path, "{\"open_hotkey\":\"Ctrl+Shift+P\",\"favorite_hotkey\":\"Ctrl+F\",\"unfavorite_hotkey\":\"Ctrl+Shift+U\",\"enable_notification\":false,\"favorite_folder_name\":\"收藏\"}");
            AppConfig unicode = AppConfig.Load(path, out warning); Check(warning == null && unicode.FavoriteFolderName == "收藏", "legal Unicode favorite folder name loads");
            string[] invalid = { "", "   ", ".", "..", "a/b", "a\\b", "a<b", "a>", "a:b", "a\"b", "a|b", "a?b", "a*b", "tail ", "tail.", "CON", "prn.txt", "AUX.foo", "NUL", "COM1", "com9.ext", "LPT1", "lpt9.txt", "COM¹", "LPT³.txt" };
            foreach (string candidate in invalid) { string invalidError; Check(!AppConfig.TryValidateFavoriteFolderName(candidate, out invalidError), "invalid folder name rejected: [" + candidate + "]"); }
            string legalError; Check(AppConfig.TryValidateFavoriteFolderName("お気に入り", out legalError), "legal Japanese folder name accepted");
            AppConfig saved = unicode.WithFavoriteFolderName("お気に入り"); saved.Save(path); AppConfig reloaded = AppConfig.Load(path, out warning);
            Check(warning == null && reloaded.FavoriteFolderName == "お気に入り", "valid favorite folder name persists");
            byte[] priorConfig = File.ReadAllBytes(path); AppConfig.BeforeConfigCommit = delegate { throw new IOException("simulated config commit failure"); };
            bool saveFailed = false; try { saved.WithFavoriteFolderName("Starred").Save(path); } catch (IOException) { saveFailed = true; } finally { AppConfig.BeforeConfigCommit = null; }
            AppConfig afterFailure = AppConfig.Load(path, out warning); Check(saveFailed && afterFailure.FavoriteFolderName == "お気に入り" && Equal(priorConfig, File.ReadAllBytes(path)), "failed save retains prior disk configuration and current value");
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
            string switchImage = Path.Combine(root, "switch.jpg"); File.WriteAllBytes(switchImage, new byte[] { 9 }); SourceItem switchItem = new SourceItem(switchImage, "switch.jpg", files.Read(switchImage)); string currentName = "A";
            FavoriteService configurable = new FavoriteService(files, new ShellShortcutStore(), delegate { return currentName; });
            Check(configurable.Execute(new FavoriteOperationRequest(FavoriteAction.Favorite, switchItem)).Changed && Directory.Exists(Path.Combine(root, "A")), "configured A directory is used");
            currentName = "B"; Check(configurable.Classify(switchItem) == FavoriteState.NotFavorited && configurable.Execute(new FavoriteOperationRequest(FavoriteAction.Favorite, switchItem)).Changed && Directory.Exists(Path.Combine(root, "B")), "A to B ignores A and uses B without migration");
            currentName = "A"; Check(configurable.Classify(switchItem) == FavoriteState.Favorited, "switching back to A naturally reuses existing A");
        }
        finally { Directory.Delete(root, true); }
        return failures == 0 ? 0 : 1;
    }

    private static bool Equal(byte[] a, byte[] b) { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }

    private sealed class BlockingStore : IShortcutStore
    {
        internal readonly ManualResetEvent Entered = new ManualResetEvent(false), Release = new ManualResetEvent(false);
        private readonly ShellShortcutStore inner = new ShellShortcutStore();
        public void Create(string target, string shortcut) { Entered.Set(); Release.WaitOne(); inner.Create(target, shortcut); }
        public string ReadRelativePath(string shortcut) { return inner.ReadRelativePath(shortcut); }
    }
}
