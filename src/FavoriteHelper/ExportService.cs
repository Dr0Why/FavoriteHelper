using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

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
        internal Action BeforeDestinationCreate;

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

                    using (AnchoredExportDirectory anchored = AnchoredExportDirectory.Open(outputDirectory, outputDirectoryIdentity))
                    {
                        if (anchored == null)
                            return Result(shortcut, target, destination, ExportStatus.Failed, "export directory changed before write");
                        if (!outputDirectoryIdentity.Equals(files.Read(outputDirectory)))
                            return Result(shortcut, target, destination, ExportStatus.Failed, "export directory changed before destination creation");

                        AnchoredOutput output;
                        try { output = anchored.CreateNew(Path.GetFileName(target), BeforeDestinationCreate); }
                        catch (DestinationExistsException)
                        {
                            return Result(shortcut, target, destination, ExportStatus.SkippedAlreadyExists, "destination already exists");
                        }
                        catch (Exception ex)
                        {
                            return Result(shortcut, target, destination, ExportStatus.Failed, "destination could not be created: " + ex.Message);
                        }

                        using (output)
                        {
                            bool complete = false;
                            try
                            {
                                if (!outputDirectoryIdentity.Equals(files.Read(outputDirectory)))
                                    return Result(shortcut, target, destination, ExportStatus.Failed, "export directory changed during destination creation");
                                source.CopyTo(output.Stream);
                                output.Stream.Flush(true);
                                if (!outputDirectoryIdentity.Equals(files.Read(outputDirectory)))
                                    return Result(shortcut, target, destination, ExportStatus.Failed, "export directory changed during write");
                                complete = true;
                            }
                            finally
                            {
                                if (!complete) output.DeleteOnClose();
                            }
                        }
                    }
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

    internal sealed class DestinationExistsException : IOException { }

    // Keeps the validated directory object open and creates only a single child name
    // relative to that handle. A later pathname replacement therefore cannot redirect
    // the create through a junction or symbolic link.
    internal sealed class AnchoredExportDirectory : IDisposable
    {
        private const uint FileReadAttributes = 0x00000080;
        private const uint GenericWrite = 0x40000000;
        private const uint DeleteAccess = 0x00010000;
        private const uint Synchronize = 0x00100000;
        private const uint OpenExisting = 3;
        private const uint FileCreate = 2;
        private const uint FileAttributeNormal = 0x00000080;
        private const uint FileFlagBackupSemantics = 0x02000000;
        private const uint FileFlagOpenReparsePoint = 0x00200000;
        private const uint FileNonDirectoryFile = 0x00000040;
        private const uint FileSynchronousIoNonAlert = 0x00000020;
        private const uint StatusObjectNameCollision = 0xC0000035;
        private static readonly IntPtr InvalidHandle = new IntPtr(-1);
        private readonly SafeFileHandle directory;

        private AnchoredExportDirectory(SafeFileHandle directory) { this.directory = directory; }

        internal static AnchoredExportDirectory Open(string path, FileIdentity expected)
        {
            IntPtr raw = CreateFile(path, FileReadAttributes | Synchronize, 7, IntPtr.Zero, OpenExisting,
                FileFlagBackupSemantics | FileFlagOpenReparsePoint, IntPtr.Zero);
            if (raw == InvalidHandle) return null;
            SafeFileHandle handle = new SafeFileHandle(raw, true);
            ByHandleFileInformation info;
            if (!GetFileInformationByHandle(handle, out info) ||
                (info.Attributes & (uint)FileAttributes.Directory) == 0 ||
                (info.Attributes & (uint)FileAttributes.ReparsePoint) != 0 ||
                expected == null || expected.VolumeSerial != info.VolumeSerial ||
                expected.FileIndex != (((ulong)info.IndexHigh << 32) | info.IndexLow))
            {
                handle.Dispose();
                return null;
            }
            return new AnchoredExportDirectory(handle);
        }

        internal AnchoredOutput CreateNew(string name, Action beforeNativeCreate)
        {
            if (String.IsNullOrEmpty(name) || !String.Equals(name, Path.GetFileName(name), StringComparison.Ordinal) ||
                name == "." || name == "..") throw new IOException("destination filename is invalid");

            IntPtr text = Marshal.StringToHGlobalUni(name);
            try
            {
                UnicodeString unicode = new UnicodeString
                {
                    Length = checked((ushort)(name.Length * 2)),
                    MaximumLength = checked((ushort)((name.Length + 1) * 2)),
                    Buffer = text
                };
                IntPtr unicodePointer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(UnicodeString)));
                try
                {
                    Marshal.StructureToPtr(unicode, unicodePointer, false);
                    ObjectAttributes attributes = new ObjectAttributes
                    {
                        Length = Marshal.SizeOf(typeof(ObjectAttributes)),
                        RootDirectory = directory.DangerousGetHandle(),
                        ObjectName = unicodePointer,
                        Attributes = 0x00000040
                    };
                    IoStatusBlock statusBlock;
                    IntPtr raw;
                    if (beforeNativeCreate != null) beforeNativeCreate();
                    uint status = NtCreateFile(out raw, GenericWrite | DeleteAccess | FileReadAttributes | Synchronize, ref attributes,
                        out statusBlock, IntPtr.Zero, FileAttributeNormal, 0, FileCreate,
                        FileNonDirectoryFile | FileSynchronousIoNonAlert | FileFlagOpenReparsePoint, IntPtr.Zero, 0);
                    if (status == StatusObjectNameCollision) throw new DestinationExistsException();
                    if ((status & 0x80000000) != 0)
                        throw new IOException(new Win32Exception((int)RtlNtStatusToDosError(status)).Message);

                    SafeFileHandle handle = new SafeFileHandle(raw, true);
                    ByHandleFileInformation info;
                    if (!GetFileInformationByHandle(handle, out info) ||
                        (info.Attributes & ((uint)FileAttributes.Directory | (uint)FileAttributes.ReparsePoint)) != 0)
                    {
                        AnchoredOutput unsafeOutput = new AnchoredOutput(handle);
                        unsafeOutput.DeleteOnClose();
                        unsafeOutput.Dispose();
                        throw new IOException("created destination is not a regular non-reparse file");
                    }
                    return new AnchoredOutput(handle);
                }
                finally { Marshal.FreeHGlobal(unicodePointer); }
            }
            finally { Marshal.FreeHGlobal(text); }
        }

        public void Dispose() { directory.Dispose(); }

        [StructLayout(LayoutKind.Sequential)] private struct FileTime { public uint Low, High; }
        [StructLayout(LayoutKind.Sequential)] private struct ByHandleFileInformation
        {
            public uint Attributes; public FileTime Creation, Access, Write;
            public uint VolumeSerial, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        }
        [StructLayout(LayoutKind.Sequential)] private struct UnicodeString
        { public ushort Length, MaximumLength; public IntPtr Buffer; }
        [StructLayout(LayoutKind.Sequential)] private struct ObjectAttributes
        { public int Length; public IntPtr RootDirectory, ObjectName; public uint Attributes; public IntPtr SecurityDescriptor, SecurityQualityOfService; }
        [StructLayout(LayoutKind.Sequential)] private struct IoStatusBlock
        { public IntPtr Status; public UIntPtr Information; }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out ByHandleFileInformation info);
        [DllImport("ntdll.dll")]
        private static extern uint NtCreateFile(out IntPtr handle, uint access, ref ObjectAttributes attributes,
            out IoStatusBlock status, IntPtr allocationSize, uint fileAttributes, uint shareAccess,
            uint createDisposition, uint createOptions, IntPtr eaBuffer, uint eaLength);
        [DllImport("ntdll.dll")]
        private static extern uint RtlNtStatusToDosError(uint status);
    }

    internal sealed class AnchoredOutput : IDisposable
    {
        private readonly SafeFileHandle handle;
        internal readonly FileStream Stream;
        internal AnchoredOutput(SafeFileHandle handle)
        {
            this.handle = handle;
            Stream = new FileStream(handle, FileAccess.Write, 4096, false);
        }
        internal void DeleteOnClose()
        {
            FileDisposition disposition = new FileDisposition { DeleteFile = true };
            SetFileInformationByHandle(handle, 4, ref disposition, (uint)Marshal.SizeOf(typeof(FileDisposition)));
        }
        public void Dispose() { Stream.Dispose(); }
        [StructLayout(LayoutKind.Sequential)] private struct FileDisposition
        { [MarshalAs(UnmanagedType.Bool)] public bool DeleteFile; }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int infoClass, ref FileDisposition info, uint size);
    }
}
