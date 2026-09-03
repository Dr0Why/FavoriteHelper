using System;
using System.IO;
using FavoriteHelper;
internal static class LoggingTests
{
    public static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "FavoriteHelper-log-" + Guid.NewGuid().ToString("N")); string path = Path.Combine(root, "logs", "app.log");
        try
        {
            const string privatePath = @"C:\Users\ExampleUser\Pictures\Private\secret-image.jpg";
            const string unicodePath = @"C:\Users\ExampleUser\Pictures\私人\秘密照片.jpg";
            const string customFolder = "我的私人收藏";
            Log.Initialize(path);
            Log.Write("START", "FavoriteHelper v6.3.0 Round 3; hotkeys=Ctrl+Shift+P,Ctrl+F,Ctrl+Shift+U");
            Log.Write("PENDING_CREATED", "id=42 items=3");
            Log.Write("SESSION_BOUND", "PhotosPid=1234");
            Log.Write("FAVORITE_ACCEPTED", "observation=7");
            Log.Write("FAVORITE_OPERATION", "state=Favorited changed=True");
            Log.Write("CONFIG_SAVED", "configuration updated");
            Log.Write("COMMAND_FATAL", Log.ErrorCategory(new IOException("Failed for " + privatePath)));
            Log.Write("WORKER_FATAL", Log.ErrorCategory(new InvalidOperationException("Unicode path " + unicodePath)));
            string payload = new string('x', 800);
            for (int i = 0; i < 2000; i++) Log.Write("BOUNDED_TEST", payload);
            Log.Close();
            FileInfo current = new FileInfo(path); FileInfo prior = new FileInfo(path + ".1");
            bool bounded = current.Exists && current.Length <= 1050000 && (!prior.Exists || prior.Length <= 1050000);
            Console.WriteLine((bounded ? "PASS " : "FAIL ") + "app-local log files rotate at bounded size");
            string text = File.ReadAllText(path); bool useful = text.Contains("BOUNDED_TEST") && !text.Contains("image/png") && !text.Contains("thumbnail");
            Console.WriteLine((useful ? "PASS " : "FAIL ") + "log is diagnostic text without image/thumbnail payloads");
            string all = text + (prior.Exists ? File.ReadAllText(prior.FullName) : String.Empty);
            bool privateValuesAbsent = !all.Contains(privatePath) && !all.Contains("ExampleUser") && !all.Contains("secret-image.jpg") &&
                !all.Contains(unicodePath) && !all.Contains("私人") && !all.Contains("秘密照片.jpg") && !all.Contains(customFolder);
            Console.WriteLine((privateValuesAbsent ? "PASS " : "FAIL ") + "normal production log details exclude paths, filenames, user names, Unicode filesystem names, and custom folder names");
            bool eventsRemain = all.Contains("START") && all.Contains("PENDING_CREATED") && all.Contains("items=3") &&
                all.Contains("SESSION_BOUND") && all.Contains("PhotosPid=1234") && all.Contains("FAVORITE_ACCEPTED") &&
                all.Contains("state=Favorited") && all.Contains("CONFIG_SAVED") && all.Contains("COMMAND_FATAL error=IOException") &&
                all.Contains("WORKER_FATAL error=InvalidOperationException");
            Console.WriteLine((eventsRemain ? "PASS " : "FAIL ") + "non-sensitive events, states, counts, process IDs, and exception categories remain useful");
            return bounded && useful && privateValuesAbsent && eventsRemain ? 0 : 1;
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
