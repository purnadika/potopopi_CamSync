using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaDevices;
using Microsoft.Extensions.Logging;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.Services
{
    public class MtpDeviceProvider : IDeviceProvider
    {
        private MediaDevice? _device;
        private readonly ILogger<MtpDeviceProvider> _logger;

        public string DeviceId { get; private set; }
        public string DeviceName { get; private set; }
        public bool IsConnected => _device is not null && _device.IsConnected;

        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".cr2", ".cr3", ".nef", ".arw", ".dng", ".mp4", ".mov", ".avi"
        };

        public MtpDeviceProvider(string deviceId, string deviceName, ILogger<MtpDeviceProvider>? logger = null)
        {
            DeviceId = deviceId;
            DeviceName = deviceName;
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MtpDeviceProvider>.Instance;
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var devices = MediaDevice.GetDevices();
                _device = null;
                foreach (var d in devices)
                {
                    if (string.Equals(d.DeviceId, DeviceId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(d.FriendlyName, DeviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        _device = d;
                        break;
                    }
                }

                if (_device is not null)
                {
                    _device.Connect();
                    _logger.LogInformation("Connected to MTP device: {Name}", DeviceName);
                }
                else
                {
                    throw new InvalidOperationException($"MTP Device not found: {DeviceName}");
                }
            }, cancellationToken);
        }

        public void Disconnect()
        {
            if (_device is not null)
            {
                try
                {
                    if (_device.IsConnected)
                        _device.Disconnect();
                    _device.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disconnecting MTP device: {Name}", DeviceName);
                }
                finally
                {
                    _device = null;
                }
            }
        }

        public Task<List<SyncFile>> GetFilesAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                var files = new List<SyncFile>();
                if (!IsConnected || _device is null) return files;

                var storages = _device.GetDrives();
                foreach (var storage in storages)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string root = storage.RootDirectory.FullName;
                    string? dcimPath = null;
                    foreach (var dir in _device.GetDirectories(root))
                    {
                        if (dir.EndsWith("DCIM", StringComparison.OrdinalIgnoreCase))
                        {
                            dcimPath = dir;
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(dcimPath))
                    {
                        FindFilesIterative(dcimPath, files, dcimPath, cancellationToken);
                    }
                }

                _logger.LogInformation("Scanned MTP device {Name}: found {Count} files", DeviceName, files.Count);
                return files;
            }, cancellationToken);
        }

        private void FindFilesIterative(string rootPath, List<SyncFile> files, string basePath, CancellationToken cancellationToken)
        {
            var stack = new Stack<string>();
            stack.Push(rootPath);

            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string currentPath = stack.Pop();

                try
                {
                    foreach (var dir in _device!.GetDirectories(currentPath))
                        stack.Push(dir);

                    foreach (var filePath in _device.GetFiles(currentPath))
                    {
                        string ext = Path.GetExtension(filePath);
                        if (!SupportedExtensions.Contains(ext)) continue;

                        try
                        {
                            var info = _device.GetFileInfo(filePath);
                            DateTime creationTime;
                            if (info.CreationTime.HasValue)
                                creationTime = info.CreationTime.Value;
                            else if (info.LastWriteTime.HasValue)
                                creationTime = info.LastWriteTime.Value;
                            else
                            {
                                _logger.LogWarning("No timestamp on file {File}, skipping.", filePath);
                                continue;
                            }

                            files.Add(new SyncFile
                            {
                                OriginalPath = filePath,
                                RelativePath = filePath.Substring(basePath.Length).TrimStart('\\', '/'),
                                FileName = Path.GetFileName(filePath),
                                Size = (long)info.Length,
                                CreationTime = creationTime
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not read file info for {File}", filePath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not traverse directory {Dir}", currentPath);
                }
            }
        }

        public Task DownloadToStreamAsync(SyncFile file, Stream destination, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_device is null) throw new InvalidOperationException("Device not connected.");
                _device.DownloadFile(file.OriginalPath, destination);
                _logger.LogDebug("Downloaded {File} from MTP device", file.FileName);
            }, cancellationToken);
        }

        public Task DeleteFileAsync(SyncFile file, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_device is null) throw new InvalidOperationException("Device not connected.");
                _device.DeleteFile(file.OriginalPath);
                _logger.LogInformation("Deleted {File} from MTP device", file.FileName);
            }, cancellationToken);
        }
    }
}
