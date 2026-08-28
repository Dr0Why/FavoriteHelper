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
            Log.Initialize(path); string payload = new string('x', 180);
            for (int i = 0; i < 20000; i++) Log.Write("BOUNDED_TEST", payload);
            Log.Close();
            FileInfo current = new FileInfo(path); FileInfo prior = new FileInfo(path + ".1");
            bool bounded = current.Exists && current.Length <= 1050000 && (!prior.Exists || prior.Length <= 1050000);
            Console.WriteLine((bounded ? "PASS " : "FAIL ") + "app-local log files rotate at bounded size");
            string text = File.ReadAllText(path); bool useful = text.Contains("BOUNDED_TEST") && !text.Contains("image/png") && !text.Contains("thumbnail");
            Console.WriteLine((useful ? "PASS " : "FAIL ") + "log is diagnostic text without image/thumbnail payloads");
            return bounded && useful ? 0 : 1;
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
