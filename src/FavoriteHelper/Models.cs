using System;
using System.Collections.Generic;

namespace FavoriteHelper
{
    internal sealed class FileIdentity : IEquatable<FileIdentity>
    {
        public readonly uint VolumeSerial;
        public readonly ulong FileIndex;

        public FileIdentity(uint volumeSerial, ulong fileIndex)
        {
            VolumeSerial = volumeSerial;
            FileIndex = fileIndex;
        }

        public bool Equals(FileIdentity other)
        {
            return other != null && VolumeSerial == other.VolumeSerial && FileIndex == other.FileIndex;
        }

        public override bool Equals(object obj) { return Equals(obj as FileIdentity); }
        public override int GetHashCode() { return VolumeSerial.GetHashCode() ^ FileIndex.GetHashCode(); }
        public override string ToString() { return String.Format("{0:X8}:{1:X16}", VolumeSerial, FileIndex); }
    }

    internal sealed class SourceItem
    {
        public readonly string FullPath;
        public readonly string Basename;
        public readonly FileIdentity Identity;

        public SourceItem(string fullPath, string basename, FileIdentity identity)
        {
            FullPath = fullPath;
            Basename = basename;
            Identity = identity;
        }
    }

    internal sealed class SourceSnapshot
    {
        public readonly IList<SourceItem> Items;
        public SourceSnapshot(IList<SourceItem> items) { Items = new List<SourceItem>(items).AsReadOnly(); }
    }

    internal sealed class PendingSession
    {
        public readonly long Id;
        public readonly DateTime CreatedUtc;
        public readonly SourceItem SelectedItem;
        public readonly SourceSnapshot Snapshot;

        public PendingSession(long id, DateTime createdUtc, SourceItem selectedItem, SourceSnapshot snapshot)
        {
            Id = id;
            CreatedUtc = createdUtc;
            SelectedItem = selectedItem;
            Snapshot = snapshot;
        }
    }

    internal sealed class BoundSession
    {
        public readonly long Id;
        public readonly uint PhotosProcessId;
        public readonly SourceSnapshot Snapshot;
        public DateTime? EmptyBasenameSinceUtc;

        public BoundSession(long id, uint photosProcessId, SourceSnapshot snapshot)
        {
            Id = id;
            PhotosProcessId = photosProcessId;
            Snapshot = snapshot;
        }
    }

    internal enum ResolveStatus { Resolved, Grace, Invalid }

    internal sealed class ResolveResult
    {
        public readonly ResolveStatus Status;
        public readonly SourceItem Item;
        public readonly string Reason;
        private ResolveResult(ResolveStatus status, SourceItem item, string reason) { Status = status; Item = item; Reason = reason; }
        public static ResolveResult Resolved(SourceItem item) { return new ResolveResult(ResolveStatus.Resolved, item, null); }
        public static ResolveResult Grace() { return new ResolveResult(ResolveStatus.Grace, null, "basename temporarily unavailable"); }
        public static ResolveResult Invalid(string reason) { return new ResolveResult(ResolveStatus.Invalid, null, reason); }
    }
}
