using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using FavoriteHelper;

internal static class RuntimeRelocationProbe
{
    private static readonly WindowsFileValidator Files = new WindowsFileValidator();
    private static readonly FavoriteService Service = new FavoriteService(Files, new ShellShortcutStore());
    private static readonly PhotosReader Photos = new PhotosReader();
    private static int passed;

    public static int Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "create-link") { new ShellShortcutStore().Create(args[1], args[2]); Console.WriteLine("CREATED " + args[2]); return 0; }
        if (args.Length != 3) throw new ArgumentException("seed sameVolumeRoot crossVolumeRoot");
        Directory.CreateDirectory(args[1]); Directory.CreateDirectory(args[2]);
        ParentMove(args[0], args[1]); Rename(args[0], args[1]); CopyDelete(args[0], args[1], "same-volume copy-delete"); CopyDelete(args[0], args[2], "cross-volume copy-delete");
        Console.WriteLine("RUNTIME RELOCATION ALL PASS (" + passed + ")"); return 0;
    }

    private static void ParentMove(string seed, string root)
    {
        string token = Guid.NewGuid().ToString("N"), name = "parent-" + token + "-图片.jpg";
        string oldParent = Path.Combine(root, "parent-old-" + token), oldDir = Path.Combine(oldParent, "images"); Directory.CreateDirectory(oldDir); File.Copy(seed, Path.Combine(oldDir, name));
        CreateFavorite(Path.Combine(oldDir, name)); string newParent = Path.Combine(root, "parent-new-" + token); Directory.Move(oldParent, newParent);
        string newDir = Path.Combine(newParent, "images"); Verify(!Directory.Exists(oldDir), "parent move old path absent"); VerifyLaunchAndClassify(Path.Combine(newDir, name), "parent move");
    }

    private static void Rename(string seed, string root)
    {
        string token = Guid.NewGuid().ToString("N"), name = "rename-" + token + "-画像.jpg";
        string oldDir = Path.Combine(root, "before-" + token); Directory.CreateDirectory(oldDir); File.Copy(seed, Path.Combine(oldDir, name)); CreateFavorite(Path.Combine(oldDir, name));
        string newDir = Path.Combine(root, "after-目录-" + token); Directory.Move(oldDir, newDir); Verify(!Directory.Exists(oldDir), "directory rename old path absent"); VerifyLaunchAndClassify(Path.Combine(newDir, name), "directory rename");
    }

    private static void CopyDelete(string seed, string destinationRoot, string label)
    {
        string token = Guid.NewGuid().ToString("N"), name = "copy-" + token + "-测试.jpg";
        string source = Path.Combine(Path.GetTempPath(), "FavoriteHelper-copy-source-" + token); Directory.CreateDirectory(source); File.Copy(seed, Path.Combine(source, name)); CreateFavorite(Path.Combine(source, name));
        string destination = Path.Combine(destinationRoot, "copy-destination-" + token); CopyTree(source, destination); Directory.Delete(source, true);
        Verify(!Directory.Exists(source), label + " old path absent"); VerifyLaunchAndClassify(Path.Combine(destination, name), label);
    }

    private static void CreateFavorite(string image)
    {
        SourceItem item = Item(image); FavoriteResult result = Service.Execute(new FavoriteOperationRequest(FavoriteAction.Favorite, item)); if (!result.Changed) throw new Exception("create failed: " + result.Message);
    }

    private static void VerifyLaunchAndClassify(string image, string label)
    {
        SourceItem item = Item(image); Verify(Service.Classify(item) == FavoriteState.Favorited, label + " post-relocation classification");
        string link = Service.ShortcutPath(item); Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
        string expected = Path.GetFileName(image); bool found = false;
        for (int i = 0; i < 80 && !found; i++) { Thread.Sleep(250); EnumWindows(delegate(IntPtr hwnd, IntPtr data) { if (WindowClassifier.Classify(hwnd) == ForegroundKind.Photos && Photos.ReadBasename(hwnd) == expected) found = true; return true; }, IntPtr.Zero); }
        Verify(found, label + " real Shell launch opened relocated basename");
    }

    private static SourceItem Item(string path) { FileIdentity id = Files.Read(path); if (id == null) throw new Exception("identity unavailable " + path); return new SourceItem(path, Path.GetFileName(path), id); }
    private static void CopyTree(string source, string destination) { Directory.CreateDirectory(destination); foreach (string f in Directory.GetFiles(source)) File.Copy(f, Path.Combine(destination, Path.GetFileName(f))); foreach (string d in Directory.GetDirectories(source)) CopyTree(d, Path.Combine(destination, Path.GetFileName(d))); }
    private static void Verify(bool condition, string name) { if (!condition) throw new Exception("FAIL " + name); Console.WriteLine("PASS " + name); passed++; }
    private delegate bool EnumProc(IntPtr hwnd, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr data);
}
