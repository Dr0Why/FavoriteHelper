using System;
using System.IO;
using System.Security.Cryptography;

namespace FavoriteHelper
{
    internal enum ShortcutMigrationStatus { Migrated, AlreadyCurrent, Refused, Failed }

    internal sealed class ShortcutMigrationResult
    {
        public readonly ShortcutMigrationStatus Status;
        public readonly string Message;
        public ShortcutMigrationResult(ShortcutMigrationStatus status, string message) { Status = status; Message = message; }
    }

    internal sealed class ShortcutMigrationService
    {
        private readonly IFileValidator files;
        private readonly IMigrationShortcutStore shortcuts;
        private readonly Func<string> favoriteFolderName;
        internal Action BeforeReplace;

        public ShortcutMigrationService(IFileValidator files, IMigrationShortcutStore shortcuts) : this(files, shortcuts, delegate { return FavoriteService.FavoritesDirectoryName; }) { }
        public ShortcutMigrationService(IFileValidator files, IMigrationShortcutStore shortcuts, Func<string> favoriteFolderName)
        {
            this.files = files;
            this.shortcuts = shortcuts;
            this.favoriteFolderName = favoriteFolderName;
        }

        public ShortcutMigrationResult Migrate(string shortcutPath)
        {
            string shortcut, target, expectedShortcut;
            FileIdentity targetIdentity, directoryIdentity, shortcutIdentity;
            byte[] shortcutDigest;
            try
            {
                shortcut = Path.GetFullPath(shortcutPath);
                if (!File.Exists(shortcut) || !String.Equals(Path.GetExtension(shortcut), ".lnk", StringComparison.OrdinalIgnoreCase)) return Refuse("shortcut does not exist");
                if ((File.GetAttributes(shortcut) & FileAttributes.ReparsePoint) != 0) return Refuse("shortcut is a reparse point");
                string relative = shortcuts.ReadRelativePath(shortcut);
                if (String.IsNullOrWhiteSpace(relative)) return Refuse("shortcut has no relative path");
                target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(shortcut), relative));
                targetIdentity = files.Read(target);
                if (targetIdentity == null) return Refuse("relative target does not exist");
                string folderName = favoriteFolderName();
                expectedShortcut = Path.Combine(Path.GetDirectoryName(target), folderName, Path.GetFileName(target) + ".lnk");
                if (!SamePath(shortcut, expectedShortcut)) return Refuse("shortcut does not match the FavoriteHelper collection structure");
                if (!TrySafeDirectory(Path.GetDirectoryName(shortcut), out directoryIdentity)) return Refuse("favorites directory is unsafe");
                SourceItem item = new SourceItem(target, Path.GetFileName(target), targetIdentity);
                if (new FavoriteService(files, shortcuts, delegate { return folderName; }).Classify(item) != FavoriteState.Favorited) return Refuse("shortcut is not a proven favorite");
                string storedTarget = shortcuts.ReadStoredTargetPath(shortcut);
                if (!String.IsNullOrWhiteSpace(storedTarget) && SamePath(storedTarget, target)) return new ShortcutMigrationResult(ShortcutMigrationStatus.AlreadyCurrent, "shortcut already uses the current target");
                shortcutIdentity = files.Read(shortcut); shortcutDigest = Hash(shortcut);
                if (shortcutIdentity == null) return Refuse("shortcut identity is unavailable");
            }
            catch { return Refuse("shortcut metadata is malformed"); }

            string token = Guid.NewGuid().ToString("N");
            string temporary = Path.Combine(Path.GetDirectoryName(shortcut), ".favoritehelper-migrate-" + token + ".tmp.lnk");
            string backup = Path.Combine(Path.GetDirectoryName(shortcut), ".favoritehelper-migrate-" + token + ".bak.lnk");
            bool committed = false;
            try
            {
                shortcuts.Create(target, temporary);
                if (!ValidFreshLink(temporary, target, targetIdentity)) return Fail("rebuilt shortcut verification failed");
                if (BeforeReplace != null) BeforeReplace();
                FileIdentity currentDirectory;
                if (!TrySafeDirectory(Path.GetDirectoryName(shortcut), out currentDirectory) || !directoryIdentity.Equals(currentDirectory)) return Fail("favorites directory changed");
                if (!targetIdentity.Equals(files.Read(target))) return Fail("relative target changed");
                if (!shortcutIdentity.Equals(files.Read(shortcut)) || !Equal(shortcutDigest, SafeHash(shortcut))) return Fail("source shortcut changed");
                File.Replace(temporary, shortcut, backup, true);
                committed = true;
                return new ShortcutMigrationResult(ShortcutMigrationStatus.Migrated, "shortcut rebuilt for the current location");
            }
            catch (Exception ex) { return Fail("shortcut rebuild failed: " + ex.Message); }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
                try { if (File.Exists(backup) && (committed || File.Exists(shortcut))) File.Delete(backup); } catch { }
            }
        }

        private bool ValidFreshLink(string shortcut, string target, FileIdentity expectedIdentity)
        {
            try
            {
                string relative = shortcuts.ReadRelativePath(shortcut);
                if (String.IsNullOrWhiteSpace(relative)) return false;
                string strict = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(shortcut), relative));
                string stored = shortcuts.ReadStoredTargetPath(shortcut);
                return SamePath(strict, target) && SamePath(stored, target) && expectedIdentity.Equals(files.Read(strict));
            }
            catch { return false; }
        }

        private bool TrySafeDirectory(string path, out FileIdentity identity)
        {
            identity = null;
            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.Directory) == 0 || (attributes & FileAttributes.ReparsePoint) != 0) return false;
                identity = files.Read(path); return identity != null;
            }
            catch { return false; }
        }

        private static bool SamePath(string a, string b) { return String.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
        private static byte[] Hash(string path) { using (SHA256 sha = SHA256.Create()) using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) return sha.ComputeHash(stream); }
        private static byte[] SafeHash(string path) { try { return Hash(path); } catch { return null; } }
        private static bool Equal(byte[] a, byte[] b) { if (a == null || b == null || a.Length != b.Length) return false; int d = 0; for (int i = 0; i < a.Length; i++) d |= a[i] ^ b[i]; return d == 0; }
        private static ShortcutMigrationResult Refuse(string message) { return new ShortcutMigrationResult(ShortcutMigrationStatus.Refused, message); }
        private static ShortcutMigrationResult Fail(string message) { return new ShortcutMigrationResult(ShortcutMigrationStatus.Failed, message); }
    }
}
