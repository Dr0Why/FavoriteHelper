using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace FavoriteHelper
{
    internal enum ExportStatus { Exported, SkippedAlreadyExists, Rejected, Failed }

    internal sealed class ExportItemResult
    {
        public readonly string ShortcutPath;
        public readonly string TargetPath;
        public readonly string DestinationPath;
        public readonly ExportStatus Status;
        public readonly string Reason;

        public ExportItemResult(string shortcutPath, string targetPath, string destinationPath, ExportStatus status, string reason)
        {
            ShortcutPath = shortcutPath;
            TargetPath = targetPath;
            DestinationPath = destinationPath;
            Status = status;
            Reason = reason;
        }
    }

    internal sealed class ExportBatchResult
    {
        public readonly IReadOnlyList<ExportItemResult> Items;
        public int ExportedCount { get; private set; }
        public int SkippedAlreadyExistsCount { get; private set; }
        public int RejectedCount { get; private set; }
        public int FailedCount { get; private set; }

        public ExportBatchResult(List<ExportItemResult> items)
        {
            Items = items.AsReadOnly();
            foreach (ExportItemResult item in items)
            {
                if (item.Status == ExportStatus.Exported) ExportedCount++;
                else if (item.Status == ExportStatus.SkippedAlreadyExists) SkippedAlreadyExistsCount++;
                else if (item.Status == ExportStatus.Rejected) RejectedCount++;
                else FailedCount++;
            }
        }
    }

    internal sealed class ExportService
    {
        internal const string ExportDirectoryName = "FavoriteHelper export";
        private readonly IFileValidator files;
        private readonly IShortcutStore shortcuts;
        private readonly Func<string> favoriteFolderName;
        internal Action BeforeWrite;

        public ExportService(IFileValidator files, IShortcutStore shortcuts) : this(files, shortcuts, delegate { return FavoriteService.FavoritesDirectoryName; }) { }
        public ExportService(IFileValidator files, IShortcutStore shortcuts, Func<string> favoriteFolderName)
        {
            this.files = files;
            this.shortcuts = shortcuts;
            this.favoriteFolderName = favoriteFolderName;
        }

        public ExportBatchResult Export(IEnumerable<string> shortcutPaths)
        {
            List<ExportItemResult> results = new List<ExportItemResult>();
            if (shortcutPaths == null)
            {
                results.Add(Result(null, null, null, ExportStatus.Rejected, "shortcut selection is missing"));
                return new ExportBatchResult(results);
            }

            foreach (string shortcutPath in shortcutPaths)
            {
                try { results.Add(ExportOne(shortcutPath)); }
                catch (Exception ex) { results.Add(Result(shortcutPath, null, null, ExportStatus.Failed, "unexpected export failure: " + ex.Message)); }
            }
            return new ExportBatchResult(results);
        }

        private ExportItemResult ExportOne(string requestedPath)
        {
            string shortcut;
            try { shortcut = Path.GetFullPath(requestedPath); }
            catch { return Result(requestedPath, null, null, ExportStatus.Rejected, "shortcut path is invalid"); }

            if (!File.Exists(shortcut) || !String.Equals(Path.GetExtension(shortcut), ".lnk", StringComparison.OrdinalIgnoreCase))
                return Result(shortcut, null, null, ExportStatus.Rejected, "input is not an existing .lnk file");

            FileIdentity shortcutIdentity;
            byte[] shortcutDigest;
            string target;
            FileIdentity targetIdentity;
            string shortcutDirectory = Path.GetDirectoryName(shortcut);
            FileIdentity shortcutDirectoryIdentity;
            try
            {
                if ((File.GetAttributes(shortcut) & FileAttributes.ReparsePoint) != 0)
                    return Result(shortcut, null, null, ExportStatus.Rejected, "shortcut is a reparse point");
                string folderName = favoriteFolderName();
                if (!String.Equals(Path.GetFileName(shortcutDirectory), folderName, StringComparison.OrdinalIgnoreCase))
                    return Result(shortcut, null, null, ExportStatus.Rejected, "shortcut is outside a FavoriteHelper favorites directory");
                if (!TrySafeDirectory(shortcutDirectory, out shortcutDirectoryIdentity))
                    return Result(shortcut, null, null, ExportStatus.Rejected, "favorites directory is unsafe");

                string relative = shortcuts.ReadRelativePath(shortcut);
                if (String.IsNullOrWhiteSpace(relative))
                    return Result(shortcut, null, null, ExportStatus.Rejected, "shortcut has no RelativePath");
                target = Path.GetFullPath(Path.Combine(shortcutDirectory, relative));
                targetIdentity = files.Read(target);
                if (targetIdentity == null)
                    return Result(shortcut, target, null, ExportStatus.Rejected, "relative target is missing or unsafe");

                string expectedShortcut = Path.Combine(Path.GetDirectoryName(target), folderName, Path.GetFileName(target) + ".lnk");
                if (!SamePath(shortcut, expectedShortcut))
                    return Result(shortcut, target, null, ExportStatus.Rejected, "shortcut does not match the FavoriteHelper directory structure");
                SourceItem item = new SourceItem(target, Path.GetFileName(target), targetIdentity);
                if (new FavoriteService(files, shortcuts, delegate { return folderName; }).Classify(item) != FavoriteState.Favorited)
                    return Result(shortcut, target, null, ExportStatus.Rejected, "shortcut is not a proven Favorited item");

                shortcutIdentity = files.Read(shortcut);
                shortcutDigest = Hash(shortcut);
                if (shortcutIdentity == null)
                    return Result(shortcut, target, null, ExportStatus.Rejected, "shortcut identity is unavailable");
            }
            catch
            {
                return Result(shortcut, null, null, ExportStatus.Rejected, "shortcut metadata is malformed or ambiguous");
            }

            string outputDirectory = Path.Combine(shortcutDirectory, ExportDirectoryName);
            string destination = Path.Combine(outputDirectory, Path.GetFileName(target));
            FileIdentity outputDirectoryIdentity;
            if (File.Exists(outputDirectory))
                return Result(shortcut, target, destination, ExportStatus.Rejected, "export path is a file");
            if (Directory.Exists(outputDirectory))
            {
                if (!TrySafeDirectory(outputDirectory, out outputDirectoryIdentity))
                    return Result(shortcut, target, destination, ExportStatus.Rejected, "export directory is unsafe");
            }
            else
            {
                try { Directory.CreateDirectory(outputDirectory); }
                catch (Exception ex) { return Result(shortcut, target, destination, ExportStatus.Failed, "export directory could not be created: " + ex.Message); }
                if (!TrySafeDirectory(outputDirectory, out outputDirectoryIdentity))
                    return Result(shortcut, target, destination, ExportStatus.Rejected, "new export directory is unsafe");
            }

            if (File.Exists(destination) || Directory.Exists(destination))
                return Result(shortcut, target, destination, ExportStatus.SkippedAlreadyExists, "destination already exists");

            try
            {
                if (BeforeWrite != null) BeforeWrite();
                using (FileStream source = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    SourceItem currentItem = new SourceItem(target, Path.GetFileName(target), targetIdentity);
                    if (!shortcutDirectoryIdentity.Equals(files.Read(shortcutDirectory)))
                        return Result(shortcut, target, destination, ExportStatus.Failed, "favorites directory changed before write");
                    if (!shortcutIdentity.Equals(files.Read(shortcut)) || !Equal(shortcutDigest, SafeHash(shortcut)) ||
                        new FavoriteService(files, shortcuts, favoriteFolderName).Classify(currentItem) != FavoriteState.Favorited)
                        return Result(shortcut, target, destination, ExportStatus.Failed, "shortcut changed before write");
                    if (!targetIdentity.Equals(files.Read(target)))
                        return Result(shortcut, target, destination, ExportStatus.Failed, "source target disappeared or changed identity");

                    FileIdentity currentOutputIdentity;
                    if (!TrySafeDirectory(outputDirectory, out currentOutputIdentity) || !outputDirectoryIdentity.Equals(currentOutputIdentity))
                        return Result(shortcut, target, destination, ExportStatus.Failed, "export directory changed before write");

                    FileStream output;
                    try { output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None); }
                    catch (IOException ex)
                    {
                        if (File.Exists(destination) || Directory.Exists(destination))
                            return Result(shortcut, target, destination, ExportStatus.SkippedAlreadyExists, "destination already exists");
                        return Result(shortcut, target, destination, ExportStatus.Failed, "destination could not be created: " + ex.Message);
                    }
                    using (output) source.CopyTo(output);
                }
                return Result(shortcut, target, destination, ExportStatus.Exported, "file exported");
            }
            catch (IOException ex)
            {
                return Result(shortcut, target, destination, ExportStatus.Failed, "copy failed: " + ex.Message);
            }
            catch (Exception ex) { return Result(shortcut, target, destination, ExportStatus.Failed, "copy failed: " + ex.Message); }
        }

        private bool TrySafeDirectory(string path, out FileIdentity identity)
        {
            identity = null;
            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.Directory) == 0 || (attributes & FileAttributes.ReparsePoint) != 0) return false;
                identity = files.Read(path);
                return identity != null;
            }
            catch { return false; }
        }

        private static ExportItemResult Result(string shortcut, string target, string destination, ExportStatus status, string reason)
        { return new ExportItemResult(shortcut, target, destination, status, reason); }
        private static bool SamePath(string a, string b)
        { return String.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
        private static byte[] Hash(string path)
        { using (SHA256 sha = SHA256.Create()) using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) return sha.ComputeHash(stream); }
        private static byte[] SafeHash(string path) { try { return Hash(path); } catch { return null; } }
        private static bool Equal(byte[] a, byte[] b)
        { if (a == null || b == null || a.Length != b.Length) return false; int d = 0; for (int i = 0; i < a.Length; i++) d |= a[i] ^ b[i]; return d == 0; }
    }
}
