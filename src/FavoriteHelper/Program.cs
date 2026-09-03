using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace FavoriteHelper
{
    internal static class Program
    {
        private static readonly ConcurrentQueue<InputAction> Actions = new ConcurrentQueue<InputAction>();
        private static readonly AutoResetEvent Wake = new AutoResetEvent(false);
        private static volatile bool stopping, accepting;
        private static readonly Dictionary<long, SourceItem> Observations = new Dictionary<long, SourceItem>();
        private static long observationVersion;
        private static TrayContext tray;
        private static AppConfig config;
        [STAThread]
        private static int Main(string[] args)
        {
            CommandRequest command = CommandLine.Parse(args);
            if (command.Mode != LaunchMode.Resident)
            {
                if (command.Mode == LaunchMode.Invalid) return CommandLine.InvalidInvocationExitCode;
                string commandBase = AppDomain.CurrentDomain.BaseDirectory;
                try
                {
                    Log.Initialize(Path.Combine(commandBase, "logs", "app.log"));
                    string commandWarning;
                    AppConfig commandConfig = AppConfig.Load(Path.Combine(commandBase, "config.json"), out commandWarning);
                    Func<string> commandFolder = delegate { return commandConfig.FavoriteFolderName; };
                    WindowsFileValidator commandFiles = new WindowsFileValidator();
                    ShellShortcutStore commandShortcuts = new ShellShortcutStore();
                    ExportService exporter = new ExportService(commandFiles, commandShortcuts, commandFolder);
                    ShortcutMigrationService migration = new ShortcutMigrationService(commandFiles, commandShortcuts, commandFolder);
                    return CommandLine.Execute(command, exporter.Export, migration.Migrate, Log.Write);
                }
                catch (Exception ex)
                {
                    try { Log.Write("COMMAND_FATAL", ex.GetType().Name + ": " + ex.Message); } catch { }
                    return CommandLine.InfrastructureFailureExitCode;
                }
                finally { try { Log.Close(); } catch { } }
            }

            bool ownsMutex;
            using (Mutex singleInstance = new Mutex(true, "Local\\FavoriteHelper-v6.1", out ownsMutex))
            {
                if (!ownsMutex) return 0;
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                Log.Initialize(Path.Combine(baseDirectory, "logs", "app.log"));
                string warning;
                try { config = AppConfig.Load(Path.Combine(baseDirectory, "config.json"), out warning); }
                catch (Exception ex) { config = AppConfig.Defaults(); warning = "Config could not be created; safe defaults loaded: " + ex.Message; }
                Log.Write("SINGLE_INSTANCE_ACQUIRED", "resident instance owns lock");
                Log.Write("START", "FavoriteHelper v" + ReleaseVersion.ProductVersion + " Round 3; base=[" + baseDirectory + "] hotkeys=" + config.Open.Text + "," + config.Favorite.Text + "," + config.Unfavorite.Text);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                tray = new TrayContext(RequestExit, CurrentFavoriteFolderName, SaveFavoriteFolderName);
                if (warning != null) { Log.Write("CONFIG_REJECTED", warning); tray.Notify(warning, true, config.EnableNotification); }
                accepting = true;
                Thread worker = new Thread(Worker) { IsBackground = false, Name = "FavoriteHelper Worker" }; worker.SetApartmentState(ApartmentState.STA); worker.Start();
                Application.Run(tray);
                worker.Join();
                Log.Write("SINGLE_INSTANCE_RELEASED", "clean exit");
                Log.Close();
            }
            return 0;
        }

        private static void RequestExit() { accepting = false; stopping = true; Wake.Set(); Log.Write("EXIT_REQUESTED", "stopping new operations and draining accepted work"); }

        private static string CurrentFavoriteFolderName() { return config.FavoriteFolderName; }
        private static string SaveFavoriteFolderName(string value)
        {
            string error;
            if (!AppConfig.TryValidateFavoriteFolderName(value, out error)) return error;
            try
            {
                AppConfig updated = config.WithFavoriteFolderName(value);
                updated.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json"));
                config = updated;
                Log.Write("CONFIG_SAVED", "favorite_folder_name=[" + value + "]");
                return null;
            }
            catch (Exception ex) { Log.Write("CONFIG_SAVE_FAILED", ex.GetType().Name + ": " + ex.Message); return "Configuration was not saved: " + ex.Message; }
        }

        private static void Worker()
        {
            WindowsFileValidator files = new WindowsFileValidator();
            SessionManager sessions = new SessionManager(files);
            ExplorerSource explorer = new ExplorerSource(files);
            PhotosReader photos = new PhotosReader();
            FavoriteOperationQueue operations = new FavoriteOperationQueue(new FavoriteService(files, new ShellShortcutStore(), CurrentFavoriteFolderName));
            try
            {
                using (KeyboardInput keyboard = new KeyboardInput(EnqueueAction, config))
                {
                    keyboard.Start();
                    IntPtr previousForeground = IntPtr.Zero;
                    while (!stopping)
                    {
                        DateTime now = DateTime.UtcNow;
                        IntPtr foreground = NativeMethods.GetForegroundWindow();
                        ForegroundKind kind = WindowClassifier.Classify(foreground);
                        if (foreground != previousForeground) { keyboard.UpdateForeground(foreground, kind, true); previousForeground = foreground; Log.Write("FOREGROUND", "kind=" + kind); }
                        else keyboard.UpdateForeground(foreground, kind, false);
                        Observe(kind, foreground, photos, sessions, keyboard, now);
                        InputAction action; while (Actions.TryDequeue(out action)) ProcessAction(action, explorer, sessions, operations, now);
                        FavoriteResult result; while ((result = operations.ExecuteNext()) != null) ReportOperation(result);
                        sessions.ExpirePending(now);
                        if (sessions.Bound != null && !WindowClassifier.IsProcessAlive(sessions.Bound.PhotosProcessId)) sessions.Invalidate("Photos process exited");
                        Wake.WaitOne(100);
                    }
                    accepting = false;
                    operations.StopAccepting();
                }
                // A mutation already executing reaches here only after Execute returned.
                FavoriteResult accepted; while ((accepted = operations.ExecuteNext()) != null) ReportOperation(accepted);
            }
            catch (Exception ex) { Log.Write("WORKER_FATAL", ex.ToString()); tray.Notify("FavoriteHelper stopped safely: " + ex.Message, true, config.EnableNotification); }
            finally
            {
                sessions.Clear("application exit"); Observations.Clear();
                Log.Write("EXIT_COMPLETE", "hook stopped, sessions released, accepted filesystem work complete");
                tray.CompleteExit();
            }
        }

        private static void EnqueueAction(InputAction action) { if (!accepting) return; Actions.Enqueue(action); Wake.Set(); }

        private static void Observe(ForegroundKind kind, IntPtr foreground, PhotosReader photos, SessionManager sessions, KeyboardInput keyboard, DateTime now)
        {
            if (kind != ForegroundKind.Photos) return;
            uint pid = WindowClassifier.PhotosPid(foreground); string basename = photos.ReadBasename(foreground);
            if (sessions.Pending != null && sessions.TryBind(pid, basename, now)) { Log.Write("SESSION_BOUND", "PhotosPid=" + pid + " basename=[" + basename + "]"); tray.Notify("Source session connected", false, config.EnableNotification); }
            if (sessions.Bound == null || sessions.Bound.PhotosProcessId != pid) return;
            ResolveResult resolved = sessions.ResolveCurrent(pid, basename, now);
            if (resolved.Status != ResolveStatus.Resolved) return;
            long version = Interlocked.Increment(ref observationVersion); Observations[version] = resolved.Item;
            while (Observations.Count > 64) Observations.Remove(version - Observations.Count + 1);
            keyboard.UpdateObservation(version);
        }

        private static void ProcessAction(InputAction action, ExplorerSource explorer, SessionManager sessions, FavoriteOperationQueue operations, DateTime now)
        {
            if (action.Kind == InputActionKind.HookDiagnostic) { Log.Write("HOOK_DIAGNOSTIC", String.Format("vk=0x{0:X2} down={1} up={2} ctrl={3} shift={4} alt={5} actualHwnd=0x{6:X} cachedHwnd=0x{7:X} cachedKind={8} suppressed={9} decision=[{10}]", action.Vk, action.Down, action.Up, action.Ctrl, action.Shift, action.Alt, action.Hwnd.ToInt64(), action.CachedHwnd.ToInt64(), action.CachedKind, action.Suppressed, action.Decision)); return; }
            if (!accepting || action.Hwnd == IntPtr.Zero) return;
            ForegroundKind actual = WindowClassifier.Classify(action.Hwnd);
            if (action.Kind == InputActionKind.ExplorerOpen)
            {
                if (actual != ForegroundKind.Explorer) return;
                SourceItem selected; SourceSnapshot snapshot; string error;
                if (!explorer.TryCaptureAndOpen(action.Hwnd, out selected, out snapshot, out error)) { Safety("OPEN_REJECTED", error); return; }
                PendingSession pending = sessions.CreatePending(selected, snapshot, now);
                Log.Write("PENDING_CREATED", "id=" + pending.Id + " selected=[" + selected.FullPath + "] items=" + snapshot.Items.Count); return;
            }
            if (actual != ForegroundKind.Photos) return;
            uint pid = WindowClassifier.PhotosPid(action.Hwnd); SourceItem observed;
            if (action.ObservationVersion == 0 || !Observations.TryGetValue(action.ObservationVersion, out observed) || sessions.Bound == null || sessions.Bound.PhotosProcessId != pid || !BelongsToBoundSnapshot(sessions.Bound, observed)) { Safety("FAVORITE_REJECTED", "No trigger-consistent source observation"); return; }
            FavoriteAction requested = action.Kind == InputActionKind.Favorite ? FavoriteAction.Favorite : FavoriteAction.Unfavorite;
            if (!operations.Enqueue(new FavoriteOperationRequest(requested, observed))) { Safety("OPERATION_REJECTED", "Application is exiting"); return; }
            Log.Write(requested == FavoriteAction.Favorite ? "FAVORITE_ACCEPTED" : "UNFAVORITE_ACCEPTED", "path=[" + observed.FullPath + "] identity=" + observed.Identity + " observation=" + action.ObservationVersion);
        }

        private static void ReportOperation(FavoriteResult result)
        {
            Log.Write("FAVORITE_OPERATION", result.Message + " state=" + result.State + " changed=" + result.Changed);
            bool safety = NotificationPolicy.IsSafety(result);
            tray.Notify(result.Message, safety, config.EnableNotification);
        }
        private static void Safety(string kind, string message) { Log.Write(kind, message); tray.Notify(message, true, config.EnableNotification); }
        private static bool BelongsToBoundSnapshot(BoundSession bound, SourceItem item) { foreach (SourceItem candidate in bound.Snapshot.Items) if (Object.ReferenceEquals(candidate, item)) return true; return false; }
    }
}
