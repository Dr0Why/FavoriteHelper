using System;
using System.Collections.Generic;

namespace FavoriteHelper
{
    internal enum LaunchMode { Resident, Export, Repair, Invalid }

    internal sealed class CommandRequest
    {
        public readonly LaunchMode Mode;
        public readonly IReadOnlyList<string> Paths;
        public readonly string Error;
        public CommandRequest(LaunchMode mode, List<string> paths, string error)
        { Mode = mode; Paths = paths.AsReadOnly(); Error = error; }
    }

    internal enum RepairBatchStatus { Repaired, AlreadyCurrent, Rejected, Failed }

    internal sealed class RepairItemResult
    {
        public readonly string ShortcutPath;
        public readonly RepairBatchStatus Status;
        public readonly string Reason;
        public RepairItemResult(string shortcutPath, RepairBatchStatus status, string reason)
        { ShortcutPath = shortcutPath; Status = status; Reason = reason; }
    }

    internal sealed class RepairBatchResult
    {
        public readonly IReadOnlyList<RepairItemResult> Items;
        public int RepairedCount { get; private set; }
        public int AlreadyCurrentCount { get; private set; }
        public int RejectedCount { get; private set; }
        public int FailedCount { get; private set; }

        public RepairBatchResult(List<RepairItemResult> items)
        {
            Items = items.AsReadOnly();
            foreach (RepairItemResult item in items)
            {
                if (item.Status == RepairBatchStatus.Repaired) RepairedCount++;
                else if (item.Status == RepairBatchStatus.AlreadyCurrent) AlreadyCurrentCount++;
                else if (item.Status == RepairBatchStatus.Rejected) RejectedCount++;
                else FailedCount++;
            }
        }
    }

    internal static class CommandLine
    {
        internal const int SuccessExitCode = 0;
        internal const int BatchFailureExitCode = 1;
        internal const int InvalidInvocationExitCode = 2;
        internal const int InfrastructureFailureExitCode = 3;

        public static CommandRequest Parse(string[] args)
        {
            if (args == null || args.Length == 0) return new CommandRequest(LaunchMode.Resident, new List<string>(), null);
            LaunchMode mode;
            if (String.Equals(args[0], "--export", StringComparison.OrdinalIgnoreCase)) mode = LaunchMode.Export;
            else if (String.Equals(args[0], "--repair", StringComparison.OrdinalIgnoreCase)) mode = LaunchMode.Repair;
            else return new CommandRequest(LaunchMode.Invalid, new List<string>(), "unknown command");
            if (args.Length == 1) return new CommandRequest(LaunchMode.Invalid, new List<string>(), "command requires at least one shortcut path");
            List<string> paths = new List<string>();
            for (int i = 1; i < args.Length; i++)
            {
                if (String.IsNullOrWhiteSpace(args[i])) return new CommandRequest(LaunchMode.Invalid, new List<string>(), "shortcut path is empty");
                paths.Add(args[i]);
            }
            return new CommandRequest(mode, paths, null);
        }

        public static int Execute(CommandRequest request, Func<IEnumerable<string>, ExportBatchResult> export,
            Func<string, ShortcutMigrationResult> repair, Action<string, string> log)
        {
            if (request == null || request.Mode == LaunchMode.Invalid || request.Mode == LaunchMode.Resident)
                return InvalidInvocationExitCode;
            try
            {
                log = log ?? delegate { };
                log("COMMAND_START", "mode=" + request.Mode + " items=" + request.Paths.Count);
                if (request.Mode == LaunchMode.Export)
                {
                    ExportBatchResult result = export(request.Paths);
                    bool failed = result.RejectedCount != 0 || result.FailedCount != 0;
                    log("COMMAND_COMPLETE", "mode=Export exported=" + result.ExportedCount + " skipped=" + result.SkippedAlreadyExistsCount + " rejected=" + result.RejectedCount + " failed=" + result.FailedCount);
                    return failed ? BatchFailureExitCode : SuccessExitCode;
                }

                RepairBatchResult repaired = Repair(request.Paths, repair, log);
                bool repairFailed = repaired.RejectedCount != 0 || repaired.FailedCount != 0;
                log("COMMAND_COMPLETE", "mode=Repair repaired=" + repaired.RepairedCount + " current=" + repaired.AlreadyCurrentCount + " rejected=" + repaired.RejectedCount + " failed=" + repaired.FailedCount);
                return repairFailed ? BatchFailureExitCode : SuccessExitCode;
            }
            catch (Exception ex)
            {
                if (log != null) log("COMMAND_FATAL", "error=" + ex.GetType().Name);
                return InfrastructureFailureExitCode;
            }
        }

        public static RepairBatchResult Repair(IEnumerable<string> paths, Func<string, ShortcutMigrationResult> migrate, Action<string, string> log)
        {
            List<RepairItemResult> results = new List<RepairItemResult>();
            foreach (string path in paths)
            {
                try
                {
                    ShortcutMigrationResult result = migrate(path);
                    RepairBatchStatus status = result.Status == ShortcutMigrationStatus.Migrated ? RepairBatchStatus.Repaired :
                        result.Status == ShortcutMigrationStatus.AlreadyCurrent ? RepairBatchStatus.AlreadyCurrent :
                        result.Status == ShortcutMigrationStatus.Refused ? RepairBatchStatus.Rejected : RepairBatchStatus.Failed;
                    results.Add(new RepairItemResult(path, status, result.Message));
                    if (status == RepairBatchStatus.Rejected || status == RepairBatchStatus.Failed)
                        (log ?? delegate { })("COMMAND_ITEM_SAFETY", "mode=Repair status=" + status);
                }
                catch (Exception ex)
                {
                    results.Add(new RepairItemResult(path, RepairBatchStatus.Failed, "repair failed: " + ex.Message));
                    (log ?? delegate { })("COMMAND_ITEM_SAFETY", "mode=Repair status=Failed error=" + ex.GetType().Name);
                }
            }
            return new RepairBatchResult(results);
        }
    }
}
