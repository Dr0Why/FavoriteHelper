using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using FavoriteHelper;

internal static class CommandRuntimeProbe
{
    private static readonly ShellShortcutStore Shortcuts = new ShellShortcutStore();
    private static int passed;
    private static void Check(bool value, string name) { if (!value) throw new Exception("FAILED: " + name); Console.WriteLine("PASS " + name); passed++; }

    public static int Main(string[] args)
    {
        if (args.Length != 1 || !File.Exists(args[0])) throw new ArgumentException("FavoriteHelper.exe path required");
        string exe = Path.GetFullPath(args[0]);
        string root = Path.Combine(Path.GetTempPath(), "FavoriteHelper-CommandRuntime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { Run(exe, root); Console.WriteLine("COMMAND RUNTIME ALL PASS (" + passed + ")"); return 0; }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    private static void Run(string exe, string root)
    {
        string exportA, linkA, exportB, linkB;
        Pair(root, "导出 one", "图像 一.jpg", new byte[] { 1, 2, 3 }, out exportA, out linkA);
        Pair(root, "导出 two", "图像 二.png", new byte[] { 4, 5, 6 }, out exportB, out linkB);
        Snapshot sourceA = Snapshot.Take(exportA), sourceB = Snapshot.Take(exportB), shortcutA = Snapshot.Take(linkA), shortcutB = Snapshot.Take(linkB);
        int exportExit = Execute(exe, "--export", linkA, linkB);
        Check(exportExit == 0, "actual executable multi-path Unicode export exits zero");
        Check(Equal(Hash(exportA), Hash(Output(linkA, Path.GetFileName(exportA)))) && Equal(Hash(exportB), Hash(Output(linkB, Path.GetFileName(exportB)))), "actual executable exports matching SHA-256 bytes");
        Check(sourceA.Same(exportA) && sourceB.Same(exportB) && shortcutA.Same(linkA) && shortcutB.Same(linkB), "actual executable leaves export sources and shortcuts unchanged");

        string oldDirectory = Path.Combine(root, "repair old"), newDirectory = Path.Combine(root, "repair 新");
        string legacySource, legacyLink; Pair(root, "repair old", "旧 图像.jpg", new byte[] { 7 }, out legacySource, out legacyLink);
        Directory.Move(oldDirectory, newDirectory); legacySource = Path.Combine(newDirectory, "旧 图像.jpg"); legacyLink = Path.Combine(newDirectory, FavoriteService.FavoritesDirectoryName, "旧 图像.jpg.lnk");
        string currentSource, currentLink; Pair(root, "repair current", "Current.jpg", new byte[] { 8 }, out currentSource, out currentLink); Snapshot currentBefore = Snapshot.Take(currentLink);
        string invalid = Path.Combine(root, "invalid.lnk"); File.WriteAllText(invalid, "malformed"); Snapshot invalidBefore = Snapshot.Take(invalid);
        Check(!Same(Shortcuts.ReadStoredTargetPath(legacyLink), legacySource), "repair fixture contains stale stored absolute target");
        int repairExit = Execute(exe, "--repair", invalid, legacyLink, currentLink);
        Check(repairExit == 1, "actual executable mixed repair batch returns one");
        Check(Same(Shortcuts.ReadStoredTargetPath(legacyLink), legacySource), "actual executable repairs legacy shortcut through migration service");
        Check(currentBefore.Same(currentLink), "actual executable AlreadyCurrent shortcut is not rewritten");
        Check(invalidBefore.Same(invalid), "actual executable rejected shortcut remains unchanged and does not stop batch");
        Check(Execute(exe, "--unknown", invalid) == 2 && Execute(exe, "--export") == 2, "actual executable invalid invocations exit two without resident fallback");

        string coexistSource, coexistLink; Pair(root, "coexist", "Coexist.jpg", new byte[] { 9 }, out coexistSource, out coexistLink);
        Process resident = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false });
        try
        {
            Thread.Sleep(1200);
            Check(!resident.HasExited, "normal resident instance remains running");
            int coexistExit = Execute(exe, "--export", coexistLink);
            Check(coexistExit == 0 && File.Exists(Output(coexistLink, "Coexist.jpg")), "one-shot export executes while resident mutex is owned");
            Thread.Sleep(300);
            Check(!resident.HasExited, "resident PID remains alive after command exits");
            int matching = 0; foreach (Process process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe))) { try { if (!process.HasExited) matching++; } catch { } finally { process.Dispose(); } }
            Check(matching == 1, "no second persistent FavoriteHelper process remains");
        }
        finally
        {
            if (!resident.HasExited) resident.Kill();
            resident.WaitForExit(); resident.Dispose();
        }
    }

    private static int Execute(string exe, params string[] args)
    {
        string arguments = String.Join(" ", Array.ConvertAll(args, Quote));
        Process process = Process.Start(new ProcessStartInfo(exe, arguments) { UseShellExecute = false, CreateNoWindow = true });
        if (!process.WaitForExit(15000)) { process.Kill(); throw new TimeoutException("command process did not exit"); }
        int code = process.ExitCode; process.Dispose(); return code;
    }
    private static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }
    private static void Pair(string root, string folder, string name, byte[] bytes, out string source, out string link)
    { string directory = Path.Combine(root, folder); Directory.CreateDirectory(directory); source = Path.Combine(directory, name); File.WriteAllBytes(source, bytes); string favorites = Path.Combine(directory, FavoriteService.FavoritesDirectoryName); Directory.CreateDirectory(favorites); link = Path.Combine(favorites, name + ".lnk"); Shortcuts.Create(source, link); }
    private static string Output(string link, string name) { return Path.Combine(Path.GetDirectoryName(link), ExportService.ExportDirectoryName, name); }
    private static bool Same(string a, string b) { return String.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
    private static byte[] Hash(string path) { using (SHA256 sha = SHA256.Create()) using (FileStream stream = File.OpenRead(path)) return sha.ComputeHash(stream); }
    private static bool Equal(byte[] a, byte[] b) { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }

    private sealed class Snapshot
    {
        private readonly byte[] hash; private readonly DateTime written; private readonly long length;
        private Snapshot(string path) { hash = Hash(path); FileInfo info = new FileInfo(path); written = info.LastWriteTimeUtc; length = info.Length; }
        public static Snapshot Take(string path) { return new Snapshot(path); }
        public bool Same(string path) { FileInfo info = new FileInfo(path); return info.Exists && length == info.Length && written == info.LastWriteTimeUtc && Equal(hash, Hash(path)); }
    }
}
