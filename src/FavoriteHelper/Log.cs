using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace FavoriteHelper
{
    internal static class Log
    {
        private const long MaxBytes = 1024 * 1024;
        private static readonly BlockingCollection<string> Lines = new BlockingCollection<string>(1024);
        private static Thread writer;
        private static string filePath;

        public static void Initialize(string path)
        {
            filePath = path;
            string directory = Path.GetDirectoryName(path); if (!String.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            Rotate();
            writer = new Thread(WriteLoop) { IsBackground = true, Name = "FavoriteHelper Log Writer" }; writer.Start();
        }
        private static void Rotate()
        {
            if (!File.Exists(filePath) || new FileInfo(filePath).Length < MaxBytes) return;
            string old = filePath + ".1"; if (File.Exists(old)) File.Delete(old); File.Move(filePath, old);
        }
        public static void Write(string kind, string detail)
        {
            string line = String.Format("{0:o} {1} {2}", DateTime.Now, kind, detail);
            if (!Lines.IsAddingCompleted) Lines.TryAdd(line);
        }
        private static void WriteLoop()
        {
            foreach (string line in Lines.GetConsumingEnumerable())
            {
                try { Rotate(); File.AppendAllText(filePath, line + Environment.NewLine); } catch { }
            }
        }
        public static void Close()
        {
            if (writer == null) return; Lines.CompleteAdding(); writer.Join(5000); writer = null;
        }
    }
}
