using System;
using System.IO;
using System.Security.Cryptography;
using FavoriteHelper;

internal class MigrationTextStore : IMigrationShortcutStore
{
    public virtual void Create(string target, string shortcut) { File.WriteAllText(shortcut, "..\\" + Path.GetFileName(target) + "\n" + Path.GetFullPath(target)); }
    public string ReadRelativePath(string shortcut) { string[] lines = Read(shortcut); return lines[0]; }
    public string ReadStoredTargetPath(string shortcut) { string[] lines = Read(shortcut); return lines[1]; }
    private static string[] Read(string path) { string value = File.ReadAllText(path); if (value == "MALFORMED") throw new InvalidDataException(); string[] lines = value.Split('\n'); if (lines.Length != 2) throw new InvalidDataException(); return lines; }
}

internal sealed class FailingMigrationStore : MigrationTextStore
{
    public override void Create(string target, string shortcut) { throw new IOException("simulated rebuild failure"); }
}

internal static class MigrationTests
{
    private static int passed;
    private static void Check(bool value, string name) { if (!value) throw new Exception("FAILED: " + name); Console.WriteLine("PASS " + name); passed++; }

    public static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "FavoriteHelper-Migration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { Run(root); Console.WriteLine("MIGRATION ALL PASS (" + passed + ")"); return 0; }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    private static void Run(string root)
    {
        WindowsFileValidator files = new WindowsFileValidator(); MigrationTextStore store = new MigrationTextStore();
        string legacyImage, legacyLink; Pair(root, "legacy", "A.jpg", out legacyImage, out legacyLink);
        File.WriteAllText(legacyLink, "..\\A.jpg\nE:\\old\\A.jpg");
        byte[] imageBefore = Hash(legacyImage); DateTime imageWrite = File.GetLastWriteTimeUtc(legacyImage);
        ShortcutMigrationResult migrated = new ShortcutMigrationService(files, store).Migrate(legacyLink);
        Check(migrated.Status == ShortcutMigrationStatus.Migrated, "eligible legacy shortcut migrated");
        Check(store.ReadRelativePath(legacyLink) == "..\\A.jpg" && Same(store.ReadStoredTargetPath(legacyLink), legacyImage), "fresh link verified against strict current target");
        Check(Equal(imageBefore, Hash(legacyImage)) && File.GetLastWriteTimeUtc(legacyImage) == imageWrite, "migration does not modify image bytes or timestamp");
        Check(Directory.GetFiles(Path.GetDirectoryName(legacyLink), ".favoritehelper-migrate-*").Length == 0, "migration temporary and backup artifacts cleaned");

        string currentImage, currentLink; Pair(root, "current", "B.jpg", out currentImage, out currentLink); store.Create(currentImage, currentLink); byte[] currentBefore = File.ReadAllBytes(currentLink);
        Check(new ShortcutMigrationService(files, store).Migrate(currentLink).Status == ShortcutMigrationStatus.AlreadyCurrent && Equal(currentBefore, File.ReadAllBytes(currentLink)), "current shortcut is an unchanged no-op");

        string missingImage, missingLink; Pair(root, "missing", "C.jpg", out missingImage, out missingLink); File.WriteAllText(missingLink, "..\\gone.jpg\nE:\\old\\gone.jpg"); byte[] missingBefore = File.ReadAllBytes(missingLink);
        Check(new ShortcutMigrationService(files, store).Migrate(missingLink).Status == ShortcutMigrationStatus.Refused && Equal(missingBefore, File.ReadAllBytes(missingLink)), "missing relative target refused unchanged");

        string malformedImage, malformedLink; Pair(root, "malformed", "D.jpg", out malformedImage, out malformedLink); File.WriteAllText(malformedLink, "MALFORMED"); byte[] malformedBefore = File.ReadAllBytes(malformedLink);
        Check(new ShortcutMigrationService(files, store).Migrate(malformedLink).Status == ShortcutMigrationStatus.Refused && Equal(malformedBefore, File.ReadAllBytes(malformedLink)), "malformed shortcut refused unchanged");

        string brokenImage, brokenLink; Pair(root, "broken", "H.jpg", out brokenImage, out brokenLink); File.WriteAllText(brokenLink, "\nE:\\old\\H.jpg"); byte[] brokenBefore = File.ReadAllBytes(brokenLink);
        Check(new ShortcutMigrationService(files, store).Migrate(brokenLink).Status == ShortcutMigrationStatus.Refused && Equal(brokenBefore, File.ReadAllBytes(brokenLink)), "Broken shortcut without relative metadata refused unchanged");

        string conflictDir = Path.Combine(root, "conflict"); Directory.CreateDirectory(Path.Combine(conflictDir, FavoriteService.FavoritesDirectoryName)); string a = Path.Combine(conflictDir, "E.jpg"), b = Path.Combine(conflictDir, "F.jpg"); File.WriteAllBytes(a, new byte[] { 1 }); File.WriteAllBytes(b, new byte[] { 2 }); string conflict = Path.Combine(conflictDir, FavoriteService.FavoritesDirectoryName, "E.jpg.lnk"); File.WriteAllText(conflict, "..\\F.jpg\nE:\\old\\F.jpg"); byte[] conflictBefore = File.ReadAllBytes(conflict);
        Check(new ShortcutMigrationService(files, store).Migrate(conflict).Status == ShortcutMigrationStatus.Refused && Equal(conflictBefore, File.ReadAllBytes(conflict)), "conflict shortcut refused unchanged");

        string unicodeImage, unicodeLink; Pair(root, "中文 日本語", "次の画像.jpg", out unicodeImage, out unicodeLink); File.WriteAllText(unicodeLink, "..\\次の画像.jpg\nE:\\旧\\次の画像.jpg");
        Check(new ShortcutMigrationService(files, store).Migrate(unicodeLink).Status == ShortcutMigrationStatus.Migrated && Same(store.ReadStoredTargetPath(unicodeLink), unicodeImage), "Unicode path migrates");

        string failedImage, failedLink; Pair(root, "failure", "G.jpg", out failedImage, out failedLink); File.WriteAllText(failedLink, "..\\G.jpg\nE:\\old\\G.jpg"); byte[] failedBefore = File.ReadAllBytes(failedLink);
        Check(new ShortcutMigrationService(files, new FailingMigrationStore()).Migrate(failedLink).Status == ShortcutMigrationStatus.Failed && Equal(failedBefore, File.ReadAllBytes(failedLink)), "rebuild failure leaves original shortcut intact");
        Check(Directory.GetFiles(Path.GetDirectoryName(failedLink), ".favoritehelper-migrate-*").Length == 0, "failed rebuild cleans temporary artifacts");

        string raceImage, raceLink; Pair(root, "race", "I.jpg", out raceImage, out raceLink); File.WriteAllText(raceLink, "..\\I.jpg\nE:\\old\\I.jpg"); ShortcutMigrationService raced = new ShortcutMigrationService(files, store);
        raced.BeforeReplace = delegate { File.WriteAllText(raceLink, "RACER\nUNCHANGED"); };
        Check(raced.Migrate(raceLink).Status == ShortcutMigrationStatus.Failed && File.ReadAllText(raceLink) == "RACER\nUNCHANGED", "source replacement before commit is not overwritten");

        string customDirectory = Path.Combine(root, "custom"); Directory.CreateDirectory(customDirectory); string customImage = Path.Combine(customDirectory, "Custom.jpg"); File.WriteAllBytes(customImage, new byte[] { 8 }); string customFavorites = Path.Combine(customDirectory, "收藏"); Directory.CreateDirectory(customFavorites); string customLink = Path.Combine(customFavorites, "Custom.jpg.lnk"); File.WriteAllText(customLink, "..\\Custom.jpg\nE:\\old\\Custom.jpg");
        Check(new ShortcutMigrationService(files, store, delegate { return "收藏"; }).Migrate(customLink).Status == ShortcutMigrationStatus.Migrated, "Repair uses configured Unicode favorite folder name");
        Check(new ShortcutMigrationService(files, store).Migrate(customLink).Status == ShortcutMigrationStatus.Refused, "Repair ignores a non-current favorite folder name");
    }

    private static void Pair(string root, string folder, string name, out string image, out string link)
    {
        string directory = Path.Combine(root, folder); Directory.CreateDirectory(Path.Combine(directory, FavoriteService.FavoritesDirectoryName)); image = Path.Combine(directory, name); File.WriteAllBytes(image, new byte[] { 3, 1, 4 }); link = Path.Combine(directory, FavoriteService.FavoritesDirectoryName, name + ".lnk");
    }
    private static bool Same(string a, string b) { return String.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
    private static byte[] Hash(string path) { using (SHA256 sha = SHA256.Create()) using (FileStream stream = File.OpenRead(path)) return sha.ComputeHash(stream); }
    private static bool Equal(byte[] a, byte[] b) { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
}
