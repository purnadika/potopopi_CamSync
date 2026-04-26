using System;
using System.IO;

namespace PotopopiCamSync.Services
{
    public static class FileLogger
    {
        private static readonly string LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        private static readonly string LogFile = Path.Combine(LogDirectory, "app.log");
        private static readonly object _lock = new object();

        public static void Log(string message)
        {
            try
            {
                lock (_lock)
                {
                    if (!Directory.Exists(LogDirectory))
                    {
                        Directory.CreateDirectory(LogDirectory);
                    }

                    // Simple rotation: if log gets bigger than 5MB, rename it
                    if (File.Exists(LogFile) && new FileInfo(LogFile).Length > 5 * 1024 * 1024)
                    {
                        File.Move(LogFile, Path.Combine(LogDirectory, $"app_{DateTime.Now:yyyyMMddHHmmss}.log"));
                    }

                    File.AppendAllText(LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // Ignore log failures
            }
        }
    }
}
