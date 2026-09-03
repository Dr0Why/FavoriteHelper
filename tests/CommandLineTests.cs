using System;
using System.Collections.Generic;
using FavoriteHelper;

internal static class CommandLineTests
{
    private static int passed;
    private static void Check(bool value, string name) { if (!value) throw new Exception("FAILED: " + name); Console.WriteLine("PASS " + name); passed++; }

    public static int Main()
    {
        Check(CommandLine.Parse(new string[0]).Mode == LaunchMode.Resident, "no arguments select resident mode");
        CommandRequest export = CommandLine.Parse(new[] { "--export", @"C:\目录 one\图像 一.jpg.lnk", @"D:\two path\二.png.lnk" });
        Check(export.Mode == LaunchMode.Export && export.Paths.Count == 2 && export.Paths[0].Contains("目录 one"), "export parses multiple Unicode and spaced literal paths");
        CommandRequest repair = CommandLine.Parse(new[] { "--repair", @"C:\旧 收藏\图像.lnk" });
        Check(repair.Mode == LaunchMode.Repair && repair.Paths.Count == 1, "repair command parses");
        Check(CommandLine.Parse(new[] { "--unknown", "x.lnk" }).Mode == LaunchMode.Invalid, "unknown switch is invalid");
        Check(CommandLine.Parse(new[] { "--export" }).Mode == LaunchMode.Invalid && CommandLine.Parse(new[] { "--repair" }).Mode == LaunchMode.Invalid, "commands with zero paths are invalid");
        Check(CommandLine.Parse(new[] { "--export", " " }).Mode == LaunchMode.Invalid, "empty selected path is invalid");

        bool exportCalled = false;
        int exportSuccess = CommandLine.Execute(export, delegate(IEnumerable<string> paths)
        {
            exportCalled = true;
            return ExportResults(ExportStatus.Exported, ExportStatus.SkippedAlreadyExists);
        }, NoRepair, null);
        Check(exportCalled && exportSuccess == 0, "command mode executes independently of resident startup and benign export states return zero");
        Check(CommandLine.Execute(export, delegate(IEnumerable<string> paths) { return ExportResults(ExportStatus.Exported, ExportStatus.Rejected); }, NoRepair, null) == 1, "mixed successful and rejected export returns one");
        Check(CommandLine.Execute(export, delegate(IEnumerable<string> paths) { return ExportResults(ExportStatus.Failed); }, NoRepair, null) == 1, "failed export maps to batch failure");

        Queue<ShortcutMigrationResult> values = new Queue<ShortcutMigrationResult>();
        values.Enqueue(new ShortcutMigrationResult(ShortcutMigrationStatus.Migrated, "repaired"));
        values.Enqueue(new ShortcutMigrationResult(ShortcutMigrationStatus.AlreadyCurrent, "current"));
        CommandRequest repairTwo = CommandLine.Parse(new[] { "--repair", "one.lnk", "two.lnk" });
        Check(CommandLine.Execute(repairTwo, NoExport, delegate(string path) { return values.Dequeue(); }, null) == 0, "repaired and AlreadyCurrent repair results return zero");

        int repairCalls = 0;
        CommandRequest repairMixed = CommandLine.Parse(new[] { "--repair", "bad.lnk", "good.lnk" });
        int repairExit = CommandLine.Execute(repairMixed, NoExport, delegate(string path)
        {
            repairCalls++;
            return repairCalls == 1 ? new ShortcutMigrationResult(ShortcutMigrationStatus.Refused, "invalid") : new ShortcutMigrationResult(ShortcutMigrationStatus.Migrated, "repaired");
        }, null);
        Check(repairExit == 1 && repairCalls == 2, "rejected repair does not stop later item and aggregate returns one");

        int thrownCalls = 0;
        RepairBatchResult caught = CommandLine.Repair(new[] { "throw.lnk", "next.lnk" }, delegate(string path)
        {
            thrownCalls++; if (thrownCalls == 1) throw new InvalidOperationException("test");
            return new ShortcutMigrationResult(ShortcutMigrationStatus.Migrated, "repaired");
        }, null);
        Check(caught.FailedCount == 1 && caught.RepairedCount == 1 && thrownCalls == 2, "per-item repair exception fails safely and processing continues");
        Check(CommandLine.Execute(export, delegate(IEnumerable<string> paths) { throw new InvalidOperationException("infrastructure"); }, NoRepair, null) == 3, "command infrastructure exception returns fatal nonzero");
        Check(CommandLine.Execute(CommandLine.Parse(new[] { "--bad" }), NoExport, NoRepair, null) == 2, "invalid invocation maps to exit code two");

        Console.WriteLine("COMMAND LINE ALL PASS (" + passed + ")");
        return 0;
    }

    private static ExportBatchResult ExportResults(params ExportStatus[] statuses)
    {
        List<ExportItemResult> items = new List<ExportItemResult>();
        foreach (ExportStatus status in statuses) items.Add(new ExportItemResult("x.lnk", null, null, status, status.ToString()));
        return new ExportBatchResult(items);
    }
    private static ExportBatchResult NoExport(IEnumerable<string> paths) { throw new Exception("unexpected export"); }
    private static ShortcutMigrationResult NoRepair(string path) { throw new Exception("unexpected repair"); }
}
