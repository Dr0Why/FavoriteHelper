using System;
using System.Diagnostics;
using System.IO;
using FavoriteHelper;

internal sealed class TextShortcutStore : IShortcutStore
{
    public bool WriteBroken;
    public void Create(string target, string shortcut) { File.WriteAllText(shortcut, WriteBroken ? "" : @"..\" + Path.GetFileName(target)); }
    public string ReadRelativePath(string shortcut) { string s = File.ReadAllText(shortcut); if (s == "CORRUPT") throw new InvalidDataException(); return s; }
}

internal static class Round2ATests
{
    private static int passed;
    private static void Check(bool value, string name) { if (!value) throw new Exception("FAILED: " + name); Console.WriteLine("PASS " + name); passed++; }
    private static SourceItem Item(string path, IFileValidator files) { return new SourceItem(path, Path.GetFileName(path), files.Read(path)); }

    public static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "FavoriteHelper-Round2A-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { Run(root); Console.WriteLine("ROUND 2A ALL PASS (" + passed + ")"); return 0; }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    private static void Run(string root)
    {
        WindowsFileValidator files = new WindowsFileValidator(); TextShortcutStore store = new TextShortcutStore();
        string aPath = Path.Combine(root, "A.jpg"), bPath = Path.Combine(root, "B.jpg");
        byte[] original = new byte[] { 1, 2, 3, 4, 5 }; File.WriteAllBytes(aPath, original); File.WriteAllBytes(bPath, new byte[] { 6 });
        SourceItem a = Item(aPath, files), b = Item(bPath, files); FavoriteService service = new FavoriteService(files, store);
        Check(service.Classify(a) == FavoriteState.NotFavorited, "four-state NotFavorited");
        FavoriteResult created = service.Execute(new FavoriteOperationRequest(FavoriteAction.Favorite, a));
        Check(created.Changed && service.Classify(a) == FavoriteState.Favorited, "four-state Favorited and safe creation");
        Check(!service.Execute(new FavoriteOperationRequest(FavoriteAction.Favorite, a)).Changed, "repeat Favorite is idempotent");
        string link = service.ShortcutPath(a); File.WriteAllText(link, "CORRUPT");
        Check(service.Classify(a) == FavoriteState.Broken, "four-state Broken");
        Check(!service.Execute(new FavoriteOperationRequest(FavoriteAction.Favorite, a)).Changed && File.ReadAllText(link) == "CORRUPT", "Broken cannot be overwritten");
        Check(!service.Execute(new FavoriteOperationRequest(FavoriteAction.Unfavorite, a)).Changed && File.Exists(link), "Broken cannot be deleted");
        File.WriteAllText(link, @"..\B.jpg");
        Check(service.Classify(a) == FavoriteState.Conflict, "four-state Conflict");
        Check(!service.Execute(new FavoriteOperationRequest(FavoriteAction.Favorite, a)).Changed && File.ReadAllText(link).EndsWith("B.jpg"), "Conflict cannot be overwritten");
        Check(!service.Execute(new FavoriteOperationRequest(FavoriteAction.Unfavorite, a)).Changed && File.Exists(link), "Conflict cannot be deleted");

        File.Delete(link); service.BeforeCommit = delegate { File.WriteAllText(link, "RACER"); };
        FavoriteResult race = service.Execute(new FavoriteOperationRequest(FavoriteAction.Favorite, a)); service.BeforeCommit = null;
        Check(!race.Changed && File.ReadAllText(link) == "RACER", "no-overwrite creation race preserves concurrent file");
        Check(Directory.GetFiles(service.DirectoryPath(a), ".favoritehelper-*.tmp.lnk").Length == 0, "temporary artifacts cleaned after commit failure");

        File.Delete(link); service.Execute(new FavoriteOperationRequest(FavoriteAction.Favorite, a));
        service.BeforeDelete = delegate { File.Delete(link); File.WriteAllText(link, "REPLACEMENT"); };
        FavoriteResult replaced = service.Execute(new FavoriteOperationRequest(FavoriteAction.Unfavorite, a)); service.BeforeDelete = null;
        Check(!replaced.Changed && File.Exists(link) && File.ReadAllText(link) == "REPLACEMENT", "delete replacement TOCTOU leaves replacement intact");
        File.Delete(link); service.Execute(new FavoriteOperationRequest(FavoriteAction.Favorite, a));
        service.BeforeDelete = delegate { File.AppendAllText(link, "MODIFIED"); };
        FavoriteResult modified = service.Execute(new FavoriteOperationRequest(FavoriteAction.Unfavorite, a)); service.BeforeDelete = null;
        Check(!modified.Changed && File.Exists(link), "delete modification TOCTOU abandons deletion");

        string outside = Path.Combine(root, "outside"); Directory.CreateDirectory(outside);
        string rpRoot = Path.Combine(root, "reparse-case"); Directory.CreateDirectory(rpRoot); string rpImage = Path.Combine(rpRoot, "R.jpg"); File.WriteAllBytes(rpImage, new byte[] { 9 });
        SourceItem rp = Item(rpImage, files); string rpDirectory = service.DirectoryPath(rp);
        Check(CreateJunction(rpDirectory, outside), "reparse test junction created");
        Check(!service.Execute(new FavoriteOperationRequest(FavoriteAction.Favorite, rp)).Changed && Directory.GetFiles(outside).Length == 0, "Reparse Point rejected without redirected write");
        Directory.Delete(rpDirectory); Directory.CreateDirectory(rpDirectory);
        service.BeforeDirectoryRevalidation = delegate
        {
            foreach (string temp in Directory.GetFiles(rpDirectory, ".favoritehelper-*.tmp.lnk")) File.Delete(temp);
            Directory.Delete(rpDirectory); if (!CreateJunction(rpDirectory, outside)) throw new Exception("junction replacement failed");
        };
        FavoriteResult revalidated = service.Execute(new FavoriteOperationRequest(FavoriteAction.Favorite, rp)); service.BeforeDirectoryRevalidation = null;
        Check(!revalidated.Changed && Directory.GetFiles(outside).Length == 0, "Reparse Point replacement rejected at revalidation");
        Directory.Delete(rpDirectory);

        string orderRoot = Path.Combine(root, "orders"); Directory.CreateDirectory(orderRoot);
        string oaPath = Path.Combine(orderRoot, "A.jpg"), obPath = Path.Combine(orderRoot, "B.jpg"); File.WriteAllBytes(oaPath, new byte[] { 1 }); File.WriteAllBytes(obPath, new byte[] { 2 });
        SourceItem oa = Item(oaPath, files), ob = Item(obPath, files); FavoriteService orderedService = new FavoriteService(files, new TextShortcutStore()); FavoriteOperationQueue queue = new FavoriteOperationQueue(orderedService);
        FavoriteOperationRequest boundA = new FavoriteOperationRequest(FavoriteAction.Favorite, oa); SourceItem current = ob; queue.Enqueue(boundA); queue.ExecuteNext();
        Check(orderedService.Classify(oa) == FavoriteState.Favorited && orderedService.Classify(current) == FavoriteState.NotFavorited, "immutable A trigger does not rebind after switch to B");
        queue.Enqueue(new FavoriteOperationRequest(FavoriteAction.Unfavorite, oa)); queue.Enqueue(new FavoriteOperationRequest(FavoriteAction.Favorite, oa)); queue.ExecuteNext(); queue.ExecuteNext();
        Check(orderedService.Classify(oa) == FavoriteState.Favorited, "Unfavorite then Favorite ordering");
        queue.Enqueue(new FavoriteOperationRequest(FavoriteAction.Unfavorite, oa)); queue.ExecuteNext(); queue.Enqueue(new FavoriteOperationRequest(FavoriteAction.Favorite, oa)); queue.Enqueue(new FavoriteOperationRequest(FavoriteAction.Unfavorite, oa)); queue.ExecuteNext(); queue.ExecuteNext();
        Check(orderedService.Classify(oa) == FavoriteState.NotFavorited, "Favorite then Unfavorite ordering");
        queue.Enqueue(new FavoriteOperationRequest(FavoriteAction.Favorite, oa)); queue.Enqueue(new FavoriteOperationRequest(FavoriteAction.Favorite, ob)); queue.ExecuteNext(); queue.ExecuteNext();
        Check(orderedService.Classify(oa) == FavoriteState.Favorited && orderedService.Classify(ob) == FavoriteState.Favorited, "multiple-image requests remain serialized and bound");

        string failPath = Path.Combine(root, "failure.jpg"); File.WriteAllBytes(failPath, new byte[] { 7 }); SourceItem fail = Item(failPath, files); TextShortcutStore brokenStore = new TextShortcutStore { WriteBroken = true }; FavoriteService brokenService = new FavoriteService(files, brokenStore);
        Check(!brokenService.Execute(new FavoriteOperationRequest(FavoriteAction.Favorite, fail)).Changed && Directory.GetFiles(brokenService.DirectoryPath(fail), ".favoritehelper-*.tmp.lnk").Length == 0, "temporary artifacts cleaned on verification failure");
        Check(Equal(original, File.ReadAllBytes(aPath)), "original image remains untouched");

        string shellRoot = Path.Combine(root, "real-shell"); Directory.CreateDirectory(shellRoot); string shellPath = Path.Combine(shellRoot, "真实 日本語.jpg"); File.WriteAllBytes(shellPath, new byte[] { 3, 1, 4 });
        SourceItem shellItem = Item(shellPath, files); FavoriteService shellService = new FavoriteService(files, new ShellShortcutStore());
        Check(shellService.Execute(new FavoriteOperationRequest(FavoriteAction.Favorite, shellItem)).Changed && shellService.Classify(shellItem) == FavoriteState.Favorited, "real Shell Link relative metadata create and read-back verification");
        Check(shellService.Execute(new FavoriteOperationRequest(FavoriteAction.Unfavorite, shellItem)).Changed && !File.Exists(shellService.ShortcutPath(shellItem)), "real Shell Link pinned deletion");
    }

    private static bool CreateJunction(string link, string target)
    {
        Process p = Process.Start(new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + link + "\" \"" + target + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
        p.WaitForExit(); return p.ExitCode == 0 && Directory.Exists(link) && (File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0;
    }
    private static bool Equal(byte[] a, byte[] b) { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
}
