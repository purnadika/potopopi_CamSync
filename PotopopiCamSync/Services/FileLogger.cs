using System;
using System.IO;

namespace PotopopiCamSync.Services
{
    public static class FileLogger
    {
        private static readonly string LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        private static readonly string InfoLogFile = Path.Combine(LogDirectory, "app.log");
        private static readonly string DebugLogFile = Path.Combine(LogDirectory, "app.debug.log");
        private static readonly object _lock = new object();

        public static void Log(string message, bool isDebugOnly = false)
        {
            try
            {
                lock (_lock)
                {
                    if (!Directory.Exists(LogDirectory))
                    {
                        Directory.CreateDirectory(LogDirectory);
                    }

                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";

                    // Always log to debug file
                    WriteWithRotation(DebugLogFile, logEntry);

                    // Log to info file only if not debug-only
                    if (!isDebugOnly)
                    {
                        WriteWithRotation(InfoLogFile, logEntry);
                    }
                }
            }
            catch
            {
                // Ignore log failures
            }
        }

        private static void WriteWithRotation(string filePath, string message)
        {
            try
            {
                // Simple rotation: if log gets bigger than 10MB, rename it
                if (File.Exists(filePath) && new FileInfo(filePath).Length > 10 * 1024 * 1024)
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    string ext = Path.GetExtension(filePath);
                    string rotatedPath = Path.Combine(LogDirectory, $"{fileName}_{DateTime.Now:yyyyMMddHHmmss}{ext}");
                    File.Move(filePath, rotatedPath);
                }

                File.AppendAllText(filePath, message);
            }
            catch
            {
                // Ignore failures to rotate/write specific file
            }
        }
    }

}
