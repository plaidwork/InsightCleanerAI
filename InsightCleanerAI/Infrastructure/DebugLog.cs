using System;
using System.IO;
using System.Text;

namespace InsightCleanerAI.Infrastructure
{
    public static class DebugLog
    {
        private static readonly object SyncRoot = new();
        private static readonly Encoding LogEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            UserConfigStore.CurrentAppFolderName,
            "logs");

        private static string LogFilePath => Path.Combine(LogDirectory, "debug.log");

        public static void Info(string message) => Write("INFO", message);

        public static void Warning(string message) => Write("WARN", message);

        public static void Error(string message, Exception? exception = null)
        {
            if (exception is null)
            {
                Write("ERROR", message);
            }
            else
            {
                Write("ERROR", $"{message} | {exception}");
            }
        }

        private static void Write(string level, string message)
        {
            try
            {
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
                lock (SyncRoot)
                {
                    Directory.CreateDirectory(LogDirectory);
                    using var stream = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
                    using var writer = new StreamWriter(stream, LogEncoding);
                    writer.Write(line);
                }
            }
            catch
            {
                // logging should never throw
            }
        }
    }
}

