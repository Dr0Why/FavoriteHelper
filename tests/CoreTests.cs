using System;
using System.Collections.Generic;
using FavoriteHelper;

internal sealed class FakeFiles : IFileValidator
{
    public readonly Dictionary<string, FileIdentity> Values = new Dictionary<string, FileIdentity>();
    public FileIdentity Read(string path) { FileIdentity value; return Values.TryGetValue(path, out value) ? value : null; }
}

internal static class CoreTests
{
    private static int passed;
    private static void Check(bool value, string name) { if (!value) throw new Exception("FAILED: " + name); Console.WriteLine("PASS " + name); passed++; }
    private static SourceItem Item(string path, uint volume, ulong index) { return new SourceItem(path, System.IO.Path.GetFileName(path), new FileIdentity(volume, index)); }

    public static int Main()
    {
        DateTime now = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc);
        FakeFiles files = new FakeFiles();
        SourceItem a = Item(@"D:\图片 日本語\a.jpg", 1, 10), b = Item(@"D:\图片 日本語\b.jpg", 1, 11);
        files.Values[a.FullPath] = a.Identity; files.Values[b.FullPath] = b.Identity;
        SourceSnapshot snapshot = new SourceSnapshot(new List<SourceItem> { a, b });
        SessionManager manager = new SessionManager(files);

        manager.CreatePending(a, snapshot, now);
        Check(!manager.TryBind(42, "b.jpg", now), "initial basename mismatch does not bind");
        Check(manager.Pending == null, "concrete mismatch consumes pending");
        Check(!manager.TryBind(42, "a.jpg", now.AddMilliseconds(1)), "failed pending cannot later revive on matching basename");
        manager.CreatePending(a, snapshot, now);
        Check(manager.TryBind(42, "a.jpg", now.AddMilliseconds(10)), "exact basename and PID bind");
        Check(manager.Pending == null && manager.Bound != null, "pending is consumed once");
        Check(manager.ResolveCurrent(42, "b.jpg", now).Item == b, "navigation resolves only snapshot item");
        Check(manager.ResolveCurrent(42, "a.jpg", now).Item == a, "return navigation resolves original");

        Check(manager.ResolveCurrent(42, "", now.AddMilliseconds(100)).Status == ResolveStatus.Grace, "transient empty basename returns no stale item");
        ResolveResult recovered = manager.ResolveCurrent(42, "a.jpg", now.AddMilliseconds(500));
        Check(recovered.Status == ResolveStatus.Resolved && recovered.Item == a, "basename recovery within grace resumes resolution");

        manager.CreatePending(b, snapshot, now);
        Check(!manager.TryBind(42, "a.jpg", now), "known old bound basename is treated as transition");
        Check(manager.Pending != null, "old bound state does not consume replacement pending");
        Check(manager.TryBind(42, "b.jpg", now.AddMilliseconds(10)), "validated replacement pending replaces old bound");
        Check(manager.ResolveCurrent(42, "", now).Status == ResolveStatus.Grace, "empty basename starts grace without stale item");
        Check(manager.ResolveCurrent(42, "", now.AddSeconds(2)).Status == ResolveStatus.Grace, "two-second boundary remains grace");
        Check(manager.ResolveCurrent(42, "", now.AddMilliseconds(2001)).Status == ResolveStatus.Invalid && manager.Bound == null, "grace expiry invalidates");

        manager.CreatePending(a, snapshot, now);
        Check(!manager.TryBind(0, "a.jpg", now), "PID zero cannot bind");
        manager.ExpirePending(now.AddSeconds(16));
        Check(manager.Pending == null && !manager.TryBind(42, "a.jpg", now.AddSeconds(16)), "expired pending cannot revive");

        manager.CreatePending(a, snapshot, now); manager.TryBind(42, "a.jpg", now);
        files.Values.Remove(b.FullPath);
        Check(manager.ResolveCurrent(42, "b.jpg", now).Status == ResolveStatus.Invalid, "missing source invalidates");
        files.Values[b.FullPath] = new FileIdentity(1, 99);
        manager.CreatePending(a, snapshot, now); manager.TryBind(42, "a.jpg", now);
        Check(manager.ResolveCurrent(42, "b.jpg", now).Status == ResolveStatus.Invalid, "replaced identity invalidates");

        SourceItem duplicate = new SourceItem(@"E:\other\a.jpg", "a.jpg", new FileIdentity(2, 20));
        files.Values[duplicate.FullPath] = duplicate.Identity;
        manager.CreatePending(a, new SourceSnapshot(new List<SourceItem> { a, duplicate }), now); manager.TryBind(42, "a.jpg", now);
        Check(manager.ResolveCurrent(42, "a.jpg", now).Status == ResolveStatus.Invalid, "duplicate basename invalidates resolution");

        Console.WriteLine("ALL PASS (" + passed + ")");
        return 0;
    }
}
