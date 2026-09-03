using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace FavoriteHelper
{
    internal enum FavoriteState { NotFavorited, Favorited, Broken, Conflict }
    internal enum FavoriteAction { Favorite, Unfavorite }

    internal sealed class FavoriteOperationRequest
    {
        public readonly FavoriteAction Action;
        public readonly SourceItem Item;
        public FavoriteOperationRequest(FavoriteAction action, SourceItem item)
        {
            Action = action;
            Item = new SourceItem(item.FullPath, item.Basename,
                new FileIdentity(item.Identity.VolumeSerial, item.Identity.FileIndex));
        }
    }

    internal sealed class FavoriteResult
    {
        public readonly bool Changed;
        public readonly FavoriteState State;
        public readonly string Message;
        public FavoriteResult(bool changed, FavoriteState state, string message) { Changed = changed; State = state; Message = message; }
    }

    internal interface IShortcutStore
    {
        void Create(string target, string shortcut);
        string ReadRelativePath(string shortcut);
    }

    internal interface IMigrationShortcutStore : IShortcutStore
    {
        string ReadStoredTargetPath(string shortcut);
    }

    internal sealed class ShellShortcutStore : IMigrationShortcutStore
    {
        public void Create(string target, string shortcut)
        {
            object value = new ShellLinkObject();
            try
            {
                IShellLinkW link = (IShellLinkW)value;
                link.SetPath(target);
                link.SetRelativePath(shortcut, 0);
                link.SetWorkingDirectory(Path.GetDirectoryName(target));
                ((IPersistFile)value).Save(shortcut, true);
            }
            finally { Marshal.FinalReleaseComObject(value); }
        }

        // Parse the StringData RelativePath field ourselves. This never invokes
        // Shell resolution, link tracking, search, or the shortcut target.
        public string ReadRelativePath(string shortcut)
        {
            byte[] bytes = File.ReadAllBytes(shortcut);
            if (bytes.Length < 76 || BitConverter.ToUInt32(bytes, 0) != 0x4c) throw new InvalidDataException("Invalid Shell Link header");
            uint flags = BitConverter.ToUInt32(bytes, 0x14);
            int p = 76;
            if ((flags & 1) != 0) { Need(bytes, p, 2); p += 2 + BitConverter.ToUInt16(bytes, p); }
            if ((flags & 2) != 0) { Need(bytes, p, 4); p += checked((int)BitConverter.ToUInt32(bytes, p)); }
            bool unicode = (flags & 0x80) != 0;
            for (int bit = 2; bit <= 6; bit++)
            {
                if ((flags & (1u << bit)) == 0) continue;
                Need(bytes, p, 2); int chars = BitConverter.ToUInt16(bytes, p); p += 2;
                int count = checked(chars * (unicode ? 2 : 1)); Need(bytes, p, count);
                string text = unicode ? Encoding.Unicode.GetString(bytes, p, count) : Encoding.Default.GetString(bytes, p, count);
                if (bit == 3) return text;
                p += count;
            }
            return null;
        }

        public string ReadStoredTargetPath(string shortcut)
        {
            object value = new ShellLinkObject();
            try
            {
                ((IPersistFile)value).Load(shortcut, 0);
                StringBuilder path = new StringBuilder(32768);
                ((IShellLinkW)value).GetPath(path, path.Capacity, IntPtr.Zero, 4); // SLGP_RAWPATH; never resolve/search.
                return path.ToString();
            }
            finally { Marshal.FinalReleaseComObject(value); }
        }
        private static void Need(byte[] bytes, int offset, int count) { if (offset < 0 || count < 0 || offset > bytes.Length - count) throw new InvalidDataException("Truncated Shell Link"); }
    }

    internal sealed class FavoriteService
    {
        internal const string FavoritesDirectoryName = "Favorite";
        private readonly IFileValidator files;
        private readonly IShortcutStore shortcuts;
        private readonly Func<string> favoriteFolderName;
        internal Action BeforeCommit;
        internal Action BeforeDelete;
        internal Action BeforeDirectoryRevalidation;

        public FavoriteService(IFileValidator files, IShortcutStore shortcuts) : this(files, shortcuts, delegate { return FavoritesDirectoryName; }) { }
        public FavoriteService(IFileValidator files, IShortcutStore shortcuts, Func<string> favoriteFolderName) { this.files = files; this.shortcuts = shortcuts; this.favoriteFolderName = favoriteFolderName; }
        public string DirectoryPath(SourceItem item) { return Path.Combine(Path.GetDirectoryName(item.FullPath), favoriteFolderName()); }
        public string ShortcutPath(SourceItem item) { return Path.Combine(DirectoryPath(item), item.Basename + ".lnk"); }

        public FavoriteState Classify(SourceItem item)
        {
            string link = ShortcutPath(item);
            if (!File.Exists(link) && !Directory.Exists(link)) return FavoriteState.NotFavorited;
            if (!File.Exists(link)) return FavoriteState.Conflict;
            string relative;
            try { relative = shortcuts.ReadRelativePath(link); }
            catch { return FavoriteState.Broken; }
            if (String.IsNullOrWhiteSpace(relative)) return FavoriteState.Broken;
            string target;
            try { target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(link), relative)); }
            catch { return FavoriteState.Broken; }
            FileIdentity targetIdentity = files.Read(target);
            if (targetIdentity == null) return FavoriteState.Broken;
            return SamePath(target, item.FullPath) && targetIdentity.Equals(item.Identity) ? FavoriteState.Favorited : FavoriteState.Conflict;
        }

        public FavoriteResult Execute(FavoriteOperationRequest request)
        {
            if (request == null || request.Item == null || request.Item.Identity == null || !request.Item.Identity.Equals(files.Read(request.Item.FullPath)))
                return new FavoriteResult(false, FavoriteState.Broken, "source identity is no longer valid");
            return request.Action == FavoriteAction.Favorite ? Create(request.Item) : Delete(request.Item);
        }

        private FavoriteResult Create(SourceItem item)
        {
            string directory = DirectoryPath(item); FileIdentity directoryIdentity;
            if (!TryEnsureSafeDirectory(directory, out directoryIdentity)) return new FavoriteResult(false, FavoriteState.Broken, "favorites directory is unsafe");
            FavoriteState state = Classify(item);
            if (state != FavoriteState.NotFavorited) return new FavoriteResult(false, state, state == FavoriteState.Favorited ? "already favorited" : "existing shortcut is not safe to overwrite");
            string temporary = Path.Combine(directory, ".favoritehelper-" + Guid.NewGuid().ToString("N") + ".tmp.lnk");
            try
            {
                shortcuts.Create(item.FullPath, temporary);
                if (ClassifyAt(item, temporary) != FavoriteState.Favorited) return new FavoriteResult(false, FavoriteState.Broken, "temporary shortcut verification failed");
                if (BeforeDirectoryRevalidation != null) BeforeDirectoryRevalidation();
                if (!IsSameSafeDirectory(directory, directoryIdentity)) return new FavoriteResult(false, FavoriteState.Broken, "favorites directory changed");
                if (BeforeCommit != null) BeforeCommit();
                // File.Move is a no-replace rename on Windows.
                File.Move(temporary, ShortcutPath(item));
                return new FavoriteResult(true, FavoriteState.Favorited, "favorite created");
            }
            catch (IOException) { return new FavoriteResult(false, Classify(item), "no-overwrite commit refused"); }
            catch (Exception ex) { return new FavoriteResult(false, FavoriteState.Broken, ex.Message); }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch { } }
        }

        private FavoriteResult Delete(SourceItem item)
        {
            string directory = DirectoryPath(item); FileIdentity directoryIdentity;
            if (!TrySafeDirectory(directory, out directoryIdentity)) return new FavoriteResult(false, FavoriteState.Broken, "favorites directory is unsafe");
            FavoriteState state = Classify(item);
            if (state != FavoriteState.Favorited) return new FavoriteResult(false, state, "only a proven favorite may be deleted");
            string link = ShortcutPath(item);
            FileIdentity identity = files.Read(link); byte[] digest;
            try { digest = Hash(link); } catch { return new FavoriteResult(false, FavoriteState.Broken, "shortcut could not be pinned"); }
            if (BeforeDirectoryRevalidation != null) BeforeDirectoryRevalidation();
            if (!IsSameSafeDirectory(directory, directoryIdentity)) return new FavoriteResult(false, FavoriteState.Broken, "favorites directory changed");
            if (BeforeDelete != null) BeforeDelete();
            if (identity == null || !identity.Equals(files.Read(link)) || !Equal(digest, SafeHash(link)) || Classify(item) != FavoriteState.Favorited)
                return new FavoriteResult(false, FavoriteState.Broken, "shortcut changed before deletion");
            // Opening without FILE_SHARE_WRITE/DELETE pins this file object against
            // replacement; disposition deletes that exact open object.
            return DeletePinned(link, identity, digest) ? new FavoriteResult(true, FavoriteState.NotFavorited, "favorite removed") : new FavoriteResult(false, FavoriteState.Broken, "safe deletion refused");
        }

        private FavoriteState ClassifyAt(SourceItem item, string link)
        {
            string relative;
            try { relative = shortcuts.ReadRelativePath(link); } catch { return FavoriteState.Broken; }
            if (String.IsNullOrWhiteSpace(relative)) return FavoriteState.Broken;
            try
            {
                string target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(link), relative));
                FileIdentity id = files.Read(target);
                return id != null && SamePath(target, item.FullPath) && id.Equals(item.Identity) ? FavoriteState.Favorited : (id == null ? FavoriteState.Broken : FavoriteState.Conflict);
            }
            catch { return FavoriteState.Broken; }
        }

        private bool TryEnsureSafeDirectory(string path, out FileIdentity identity)
        {
            identity = null;
            try { if (!Directory.Exists(path)) Directory.CreateDirectory(path); } catch { return false; }
            return TrySafeDirectory(path, out identity);
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
        private bool IsSameSafeDirectory(string path, FileIdentity expected) { FileIdentity current; return TrySafeDirectory(path, out current) && expected.Equals(current); }
        private static bool SamePath(string a, string b) { return String.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
        private static byte[] Hash(string path) { using (SHA256 sha = SHA256.Create()) using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) return sha.ComputeHash(stream); }
        private static byte[] SafeHash(string path) { try { return Hash(path); } catch { return null; } }
        private static bool Equal(byte[] a, byte[] b) { if (a == null || b == null || a.Length != b.Length) return false; int d = 0; for (int i = 0; i < a.Length; i++) d |= a[i] ^ b[i]; return d == 0; }

        private static bool DeletePinned(string path, FileIdentity expected, byte[] digest)
        {
            IntPtr h = CreateFile(path, 0x00010000 | 0x80000000, 1, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
            if (h == new IntPtr(-1)) return false;
            try
            {
                ByHandleFileInformation info; if (!GetFileInformationByHandle(h, out info)) return false;
                FileIdentity actual = new FileIdentity(info.VolumeSerial, ((ulong)info.IndexHigh << 32) | info.IndexLow);
                if (!expected.Equals(actual)) return false;
                byte[] current; using (FileStream stream = new FileStream(new Microsoft.Win32.SafeHandles.SafeFileHandle(h, false), FileAccess.Read)) using (SHA256 sha = SHA256.Create()) current = sha.ComputeHash(stream);
                if (!Equal(digest, current)) return false;
                FileDisposition disposition = new FileDisposition { DeleteFile = true };
                return SetFileInformationByHandle(h, 4, ref disposition, 1);
            }
            finally { CloseHandle(h); }
        }

        [StructLayout(LayoutKind.Sequential)] private struct FileTime { public uint Low, High; }
        [StructLayout(LayoutKind.Sequential)] private struct ByHandleFileInformation { public uint Attributes; public FileTime Creation, Access, Write; public uint VolumeSerial, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
        [StructLayout(LayoutKind.Sequential)] private struct FileDisposition { [MarshalAs(UnmanagedType.Bool)] public bool DeleteFile; }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetFileInformationByHandle(IntPtr handle, out ByHandleFileInformation info);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetFileInformationByHandle(IntPtr handle, int infoClass, ref FileDisposition info, uint size);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    }

    internal sealed class FavoriteOperationQueue
    {
        private readonly Queue<FavoriteOperationRequest> queue = new Queue<FavoriteOperationRequest>();
        private readonly FavoriteService service;
        private bool accepting = true;
        public FavoriteOperationQueue(FavoriteService service) { this.service = service; }
        public bool Enqueue(FavoriteOperationRequest request) { lock (queue) { if (!accepting) return false; queue.Enqueue(request); return true; } }
        public void StopAccepting() { lock (queue) accepting = false; }
        public FavoriteResult ExecuteNext() { FavoriteOperationRequest r; lock (queue) { if (queue.Count == 0) return null; r = queue.Dequeue(); } return service.Execute(r); }
        public int Count { get { lock (queue) return queue.Count; } }
    }
}
