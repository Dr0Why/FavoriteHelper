using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using FavoriteHelper;

internal static class ExportTests
{
    private static int passed;
    private static readonly WindowsFileValidator Files = new WindowsFileValidator();
    private static readonly ShellShortcutStore Shortcuts = new ShellShortcutStore();
    private static void Check(bool value, string name) { if (!value) throw new Exception("FAILED: " + name); Console.WriteLine("PASS " + name); passed++; }

    public static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "FavoriteHelper-Export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { Run(root); Console.WriteLine("EXPORT ALL PASS (" + passed + ")"); return 0; }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    private static void Run(string root)
    {
        string validSource, validLink; Pair(root, "valid", "A.jpg", new byte[] { 1, 2, 3 }, out validSource, out validLink);
        Snapshot sourceBefore = Snapshot.Take(validSource), linkBefore = Snapshot.Take(validLink);
        ExportBatchResult valid = Service().Export(new[] { validLink });
        string validOutput = Output(validLink, "A.jpg");
        Check(valid.ExportedCount == 1 && File.Exists(validOutput) && Equal(Hash(validSource), Hash(validOutput)), "current real FavoriteHelper shortcut exports matching bytes");
        Check(sourceBefore.Same(validSource) && linkBefore.Same(validLink), "successful export leaves source and shortcut unchanged");

        string sameA, sameALink, sameB, sameBLink;
        Pair(root, "same", "One.png", new byte[] { 4 }, out sameA, out sameALink);
        AddPair(Path.GetDirectoryName(sameA), "Two.gif", new byte[] { 5 }, out sameB, out sameBLink);
        ExportBatchResult same = Service().Export(new[] { sameALink, sameBLink });
        Check(same.ExportedCount == 2 && File.Exists(Output(sameALink, "One.png")) && File.Exists(Output(sameBLink, "Two.gif")), "multiple shortcuts in one favorites directory export");

        string multiA, multiALink, multiB, multiBLink;
        Pair(root, "multi-a", "A.jpg", new byte[] { 6 }, out multiA, out multiALink);
        Pair(root, "multi-b", "B.jpg", new byte[] { 7 }, out multiB, out multiBLink);
        ExportBatchResult multi = Service().Export(new[] { multiALink, multiBLink });
        Check(multi.ExportedCount == 2 && File.Exists(Output(multiALink, "A.jpg")) && File.Exists(Output(multiBLink, "B.jpg")), "shortcuts from multiple directories use separate outputs");

        string unicodeSource, unicodeLink; Pair(root, "中文 日本語", "图像 次.jpg", new byte[] { 8, 9 }, out unicodeSource, out unicodeLink);
        Check(Service().Export(new[] { unicodeLink }).ExportedCount == 1 && File.Exists(Output(unicodeLink, "图像 次.jpg")), "Unicode paths and filenames export");

        string legacyOld = Path.Combine(root, "legacy-old"), legacyNew = Path.Combine(root, "legacy-new");
        string legacySource, legacyLink; Pair(root, "legacy-old", "Legacy.jpg", new byte[] { 10 }, out legacySource, out legacyLink);
        Directory.Move(legacyOld, legacyNew); legacySource = Path.Combine(legacyNew, "Legacy.jpg"); legacyLink = Path.Combine(legacyNew, FavoriteService.FavoritesDirectoryName, "Legacy.jpg.lnk");
        Check(!Same(Shortcuts.ReadStoredTargetPath(legacyLink), legacySource) && Service().Export(new[] { legacyLink }).ExportedCount == 1, "legacy real shortcut with stale absolute target exports by RelativePath");

        string malformedSource, malformedLink; Pair(root, "malformed", "M.jpg", new byte[] { 11 }, out malformedSource, out malformedLink); File.WriteAllText(malformedLink, "not a shell link"); Snapshot malformedBefore = Snapshot.Take(malformedLink);
        Check(Service().Export(new[] { malformedLink }).RejectedCount == 1 && malformedBefore.Same(malformedLink) && !Directory.Exists(ExportDirectory(malformedLink)), "malformed shortcut rejected unchanged without output directory");

        string missingRelativeSource, missingRelativeLink; Pair(root, "missing-relative", "R.jpg", new byte[] { 12 }, out missingRelativeSource, out missingRelativeLink); File.WriteAllBytes(missingRelativeLink, new byte[76]); byte[] header = File.ReadAllBytes(missingRelativeLink); BitConverter.GetBytes(0x4c).CopyTo(header, 0); File.WriteAllBytes(missingRelativeLink, header);
        Check(Service().Export(new[] { missingRelativeLink }).RejectedCount == 1 && !Directory.Exists(ExportDirectory(missingRelativeLink)), "shortcut without RelativePath rejected lazily");

        string missingSource, missingLink; Pair(root, "missing-target", "Gone.jpg", new byte[] { 13 }, out missingSource, out missingLink); File.Delete(missingSource); Snapshot missingLinkBefore = Snapshot.Take(missingLink);
        Check(Service().Export(new[] { missingLink }).RejectedCount == 1 && missingLinkBefore.Same(missingLink) && !Directory.Exists(ExportDirectory(missingLink)), "missing target rejected unchanged");

        string wrongSource, wrongLink; Pair(root, "wrong", "Wrong.jpg", new byte[] { 14 }, out wrongSource, out wrongLink); string foreignDirectory = Path.Combine(Path.GetDirectoryName(wrongSource), "foreign"); Directory.CreateDirectory(foreignDirectory); string foreignLink = Path.Combine(foreignDirectory, "Wrong.jpg.lnk"); Shortcuts.Create(wrongSource, foreignLink);
        Check(Service().Export(new[] { foreignLink }).RejectedCount == 1 && !Directory.Exists(ExportDirectory(foreignLink)), "wrong non-FavoriteHelper structure rejected");

        string conflictA, conflictALink, conflictB, conflictBLink; Pair(root, "conflict", "A.jpg", new byte[] { 15 }, out conflictA, out conflictALink); AddPair(Path.GetDirectoryName(conflictA), "B.jpg", new byte[] { 16 }, out conflictB, out conflictBLink); File.Delete(conflictALink); File.Move(conflictBLink, conflictALink); Snapshot conflictBefore = Snapshot.Take(conflictALink);
        Check(Service().Export(new[] { conflictALink }).RejectedCount == 1 && conflictBefore.Same(conflictALink) && !Directory.Exists(ExportDirectory(conflictALink)), "Conflict shortcut rejected unchanged");

        File.WriteAllBytes(validOutput, new byte[] { 99 }); byte[] existing = Hash(validOutput);
        ExportBatchResult skipped = Service().Export(new[] { validLink });
        Check(skipped.SkippedAlreadyExistsCount == 1 && Equal(existing, Hash(validOutput)), "existing destination skipped and never overwritten");

        string reparseSource, reparseLink; Pair(root, "reparse", "J.jpg", new byte[] { 17 }, out reparseSource, out reparseLink); string outside = Path.Combine(root, "outside"); Directory.CreateDirectory(outside);
        Check(CreateJunction(ExportDirectory(reparseLink), outside), "export reparse test junction created");
        Check(Service().Export(new[] { reparseLink }).RejectedCount == 1 && Directory.GetFiles(outside).Length == 0, "output directory reparse point rejected without redirected write");

        string independentSource, independentLink; Pair(root, "independent", "Good.jpg", new byte[] { 18 }, out independentSource, out independentLink);
        ExportBatchResult independent = Service().Export(new[] { malformedLink, independentLink });
        Check(independent.RejectedCount == 1 && independent.ExportedCount == 1 && File.Exists(Output(independentLink, "Good.jpg")), "rejected item does not stop unrelated export");

        string raceSource, raceLink; Pair(root, "source-race", "Race.jpg", new byte[] { 19 }, out raceSource, out raceLink); Snapshot raceLinkBefore = Snapshot.Take(raceLink);
        ExportService raced = Service(); raced.BeforeWrite = delegate { File.Delete(raceSource); File.WriteAllBytes(raceSource, new byte[] { 20 }); };
        ExportBatchResult race = raced.Export(new[] { raceLink });
        Check(race.FailedCount == 1 && !File.Exists(Output(raceLink, "Race.jpg")) && raceLinkBefore.Same(raceLink), "source identity replacement before write fails closed");

        string customDirectory = Path.Combine(root, "custom"); Directory.CreateDirectory(customDirectory); string customSource = Path.Combine(customDirectory, "Custom.jpg"); File.WriteAllBytes(customSource, new byte[] { 21 }); string customFavorites = Path.Combine(customDirectory, "お気に入り"); Directory.CreateDirectory(customFavorites); string customLink = Path.Combine(customFavorites, "Custom.jpg.lnk"); Shortcuts.Create(customSource, customLink);
        ExportService customService = new ExportService(Files, Shortcuts, delegate { return "お気に入り"; });
        Check(customService.Export(new[] { customLink }).ExportedCount == 1, "Export uses configured Unicode favorite folder name");
        Check(Service().Export(new[] { customLink }).RejectedCount == 1, "Export ignores a non-current favorite folder name");
    }

    private static ExportService Service() { return new ExportService(Files, Shortcuts); }
    private static void Pair(string root, string folder, string name, byte[] bytes, out string source, out string link)
    { string directory = Path.Combine(root, folder); Directory.CreateDirectory(directory); AddPair(directory, name, bytes, out source, out link); }
    private static void AddPair(string directory, string name, byte[] bytes, out string source, out string link)
    { source = Path.Combine(directory, name); File.WriteAllBytes(source, bytes); string favorites = Path.Combine(directory, FavoriteService.FavoritesDirectoryName); Directory.CreateDirectory(favorites); link = Path.Combine(favorites, name + ".lnk"); Shortcuts.Create(source, link); }
    private static string ExportDirectory(string link) { return Path.Combine(Path.GetDirectoryName(link), ExportService.ExportDirectoryName); }
    private static string Output(string link, string name) { return Path.Combine(ExportDirectory(link), name); }
    private static bool CreateJunction(string link, string target)
    { Process p = Process.Start(new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + link + "\" \"" + target + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }); p.WaitForExit(); return p.ExitCode == 0 && Directory.Exists(link) && (File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0; }
    private static bool Same(string a, string b) { return String.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
    private static byte[] Hash(string path) { using (SHA256 sha = SHA256.Create()) using (FileStream stream = File.OpenRead(path)) return sha.ComputeHash(stream); }
    private static bool Equal(byte[] a, byte[] b) { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }

    private sealed class Snapshot
    {
        private readonly byte[] hash; private readonly long length; private readonly DateTime created, written; private readonly FileAttributes attributes; private readonly FileIdentity identity;
        private Snapshot(string path) { hash = Hash(path); FileInfo info = new FileInfo(path); length = info.Length; created = info.CreationTimeUtc; written = info.LastWriteTimeUtc; attributes = info.Attributes; identity = Files.Read(path); }
        public static Snapshot Take(string path) { return new Snapshot(path); }
        public bool Same(string path) { FileInfo info = new FileInfo(path); return info.Exists && length == info.Length && created == info.CreationTimeUtc && written == info.LastWriteTimeUtc && attributes == info.Attributes && identity.Equals(Files.Read(path)) && Equal(hash, Hash(path)); }
    }
}
