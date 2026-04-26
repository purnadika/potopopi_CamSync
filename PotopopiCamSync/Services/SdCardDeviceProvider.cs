using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.Services
{
    public class SdCardDeviceProvider : IDeviceProvider
    {
        private readonly string _drivePath;

        public string DeviceId { get; private set; }
        public string DeviceName { get; private set; }
        public bool IsConnected => Directory.Exists(_drivePath);

        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".cr2", ".cr3", ".nef", ".arw", ".dng", ".mp4", ".mov", ".avi"
        };

        public SdCardDeviceProvider(string deviceId, string deviceName, string drivePath)
        {
            DeviceId = deviceId;
            DeviceName = deviceName;
            _drivePath = drivePath;
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Disconnect() { }

        public Task<List<SyncFile>> GetFilesAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                var files = new List<SyncFile>();
                if (!IsConnected) return files;

                string dcimPath = Path.Combine(_drivePath, "DCIM");
                if (!Directory.Exists(dcimPath)) return files;

                // Iterative traversal — no recursion
                var stack = new Stack<string>();
                stack.Push(dcimPath);

                while (stack.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string current = stack.Pop();

                    try
                    {
                        foreach (var dir in Directory.GetDirectories(current))
                            stack.Push(dir);

                        foreach (var filePath in Directory.GetFiles(current))
                        {
                            string ext = Path.GetExtension(filePath);
                            if (!SupportedExtensions.Contains(ext)) continue;

                            var info = new FileInfo(filePath);
                            files.Add(new SyncFile
                            {
                                OriginalPath = filePath,
                                RelativePath = filePath.Substring(dcimPath.Length).TrimStart('\\', '/'),
                                FileName = info.Name,
                                Size = info.Length,
                                CreationTime = info.LastWriteTime
                            });
                        }
                    }
                    catch { /* Ignore inaccessible directories */ }
                }

                return files;
            }, cancellationToken);
        }

        public Task DownloadToStreamAsync(SyncFile file, Stream destination, CancellationToken cancellationToken = default)
        {
            return Task.Run(async () =>
            {
                using var fs = File.OpenRead(file.OriginalPath);
                await fs.CopyToAsync(destination, cancellationToken);
            }, cancellationToken);
        }

        public Task DeleteFileAsync(SyncFile file, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(file.OriginalPath))
                    File.Delete(file.OriginalPath);
            }, cancellationToken);
        }
    }
}
