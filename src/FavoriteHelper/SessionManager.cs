using System;

namespace FavoriteHelper
{
    internal sealed class SessionManager
    {
        internal static readonly TimeSpan PendingLifetime = TimeSpan.FromSeconds(15);
        internal static readonly TimeSpan EmptyBasenameGrace = TimeSpan.FromSeconds(2);
        private readonly IFileValidator files;
        private long nextId;

        public PendingSession Pending { get; private set; }
        public BoundSession Bound { get; private set; }

        public SessionManager(IFileValidator files) { this.files = files; }

        public PendingSession CreatePending(SourceItem selected, SourceSnapshot snapshot, DateTime nowUtc)
        {
            Pending = new PendingSession(++nextId, nowUtc, selected, snapshot);
            return Pending;
        }

        public bool TryBind(uint photosPid, string basename, DateTime nowUtc)
        {
            PendingSession pending = Pending;
            if (pending == null || photosPid == 0) return false;
            if (nowUtc - pending.CreatedUtc > PendingLifetime)
            {
                Pending = null;
                return false;
            }
            if (String.IsNullOrEmpty(basename)) return false;
            if (!String.Equals(basename, pending.SelectedItem.Basename, StringComparison.Ordinal))
            {
                // A pre-existing bound Photos instance may still expose its old image while
                // InvokeVerbOnSelection is being delivered. That known old state is not a
                // binding attempt. Any other concrete mismatch consumes this pending session.
                if (!MapsUniquely(Bound, photosPid, basename)) Pending = null;
                return false;
            }
            FileIdentity current = files.Read(pending.SelectedItem.FullPath);
            if (current == null || !current.Equals(pending.SelectedItem.Identity))
            {
                Pending = null;
                return false;
            }
            Bound = new BoundSession(pending.Id, photosPid, pending.Snapshot);
            Pending = null;
            return true;
        }

        private static bool MapsUniquely(BoundSession bound, uint photosPid, string basename)
        {
            if (bound == null || bound.PhotosProcessId != photosPid) return false;
            int count = 0;
            foreach (SourceItem item in bound.Snapshot.Items)
                if (String.Equals(item.Basename, basename, StringComparison.Ordinal)) count++;
            return count == 1;
        }

        public void ExpirePending(DateTime nowUtc)
        {
            if (Pending != null && nowUtc - Pending.CreatedUtc > PendingLifetime) Pending = null;
        }

        public ResolveResult ResolveCurrent(uint photosPid, string basename, DateTime nowUtc)
        {
            BoundSession bound = Bound;
            if (bound == null) return ResolveResult.Invalid("no bound session");
            if (photosPid == 0 || bound.PhotosProcessId != photosPid) return ResolveResult.Invalid("Photos PID mismatch");
            if (String.IsNullOrEmpty(basename))
            {
                if (!bound.EmptyBasenameSinceUtc.HasValue) bound.EmptyBasenameSinceUtc = nowUtc;
                if (nowUtc - bound.EmptyBasenameSinceUtc.Value <= EmptyBasenameGrace) return ResolveResult.Grace();
                Invalidate("basename unavailable beyond grace");
                return ResolveResult.Invalid("basename unavailable beyond grace");
            }
            bound.EmptyBasenameSinceUtc = null;
            SourceItem match = null;
            int count = 0;
            foreach (SourceItem item in bound.Snapshot.Items)
            {
                if (String.Equals(item.Basename, basename, StringComparison.Ordinal)) { match = item; count++; }
            }
            if (count != 1)
            {
                Invalidate("basename match count " + count);
                return ResolveResult.Invalid("basename match count " + count);
            }
            FileIdentity current = files.Read(match.FullPath);
            if (current == null || !current.Equals(match.Identity))
            {
                Invalidate("source missing or identity changed");
                return ResolveResult.Invalid("source missing or identity changed");
            }
            return ResolveResult.Resolved(match);
        }

        public void Invalidate(string reason)
        {
            if (Bound != null) Log.Write("SESSION_INVALIDATED", reason);
            Bound = null;
        }

        public void Clear(string reason)
        {
            Pending = null;
            Invalidate(reason);
        }
    }
}
