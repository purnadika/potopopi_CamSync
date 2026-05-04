using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaDevices;
using Microsoft.Extensions.Logging;
using PotopopiCamSync.Models;
using PotopopiCamSync.Utilities;

namespace PotopopiCamSync.Services
{
    public class MtpDeviceProvider : IDeviceProvider
    {
        private MediaDevice? _device;
        private readonly ILogger<MtpDeviceProvider> _logger;

        public string DeviceId { get; private set; }
        public string DeviceName { get; private set; }
        public string DisplayName => $"{DeviceName} ({DeviceId})";
        public bool IsConnected => _device is not null && _device.IsConnected;



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

        public Task<List<SyncFileModel>> GetFilesAsync(CancellationToken cancellationToken = default, System.Action<string>? progressCallback = null)
        {
            return Task.Run(() =>
            {
                var files = new List<SyncFileModel>();
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
                        FindFilesIterative(dcimPath, files, dcimPath, cancellationToken, progressCallback);
                    }
                }

                _logger.LogInformation("Scanned MTP device {Name}: found {Count} files", DeviceName, files.Count);
                return files;
            }, cancellationToken);
        }

        private void FindFilesIterative(string rootPath, List<SyncFileModel> files, string basePath, CancellationToken cancellationToken, System.Action<string>? progressCallback)
        {
            var stack = new Stack<MediaDirectoryInfo>();
            
            try
            {
                var rootDirInfo = _device!.GetDirectoryInfo(rootPath);
                stack.Push(rootDirInfo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get root directory info for {Dir}", rootPath);
                return;
            }

            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var currentDir = stack.Pop();

                try
                {
                    progressCallback?.Invoke($"Scanning folder: {currentDir.Name}");

                    // Push subdirectories
                    foreach (var dir in currentDir.EnumerateDirectories())
                    {
                        stack.Push(dir);
                    }

                    // Process files
                    foreach (var fileInfo in currentDir.EnumerateFiles())
                    {
                        if (!FileUtilities.IsSupportedMedia(fileInfo.Name)) continue;

                        try
                        {
                            DateTime creationTime;
                            if (fileInfo.CreationTime.HasValue)
                                creationTime = fileInfo.CreationTime.Value;
                            else if (fileInfo.LastWriteTime.HasValue)
                                creationTime = fileInfo.LastWriteTime.Value;
                            else
                            {
                                _logger.LogWarning("No timestamp on file {File}, skipping.", fileInfo.FullName);
                                continue;
                            }

                            string relativePath = fileInfo.FullName.Substring(basePath.Length).TrimStart('\\', '/');
                            
                            files.Add(new SyncFileModel
                            {
                                OriginalPath = fileInfo.FullName,
                                RelativePath = relativePath,
                                FileName = fileInfo.Name,
                                Size = (long)fileInfo.Length,
                                CreationTime = creationTime
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not read file info for {File}", fileInfo.FullName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not traverse directory {Dir}", currentDir.FullName);
                }
            }
        }

        public Task DownloadToStreamAsync(SyncFileModel file, Stream destination, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_device is null) throw new InvalidOperationException("Device not connected.");
                _device.DownloadFile(file.OriginalPath, destination);
                _logger.LogDebug("Downloaded {File} from MTP device", file.FileName);
            }, cancellationToken);
        }

        public Task DeleteFileAsync(SyncFileModel file, CancellationToken cancellationToken = default)
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
