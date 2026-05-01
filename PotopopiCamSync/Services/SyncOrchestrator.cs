using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.Services
{
    public class SyncOrchestrator
    {
        private readonly SettingsService _settingsService;
        private readonly ILogger<SyncOrchestrator> _logger;
        private readonly ILoggerFactory _loggerFactory;

        // Untuk parallel pipeline
        private class SyncFileJob
        {
            public SyncFile File { get; set; } = null!;
            public string LocalPath { get; set; } = null!;
            public bool DownloadSuccess { get; set; }
        }

        public event Action<string>? OnSyncProgress;
        public event Action<string>? OnSyncCompleted;
        public event Action<SyncMetrics>? OnMetricsUpdated;

        public SyncOrchestrator(SettingsService settingsService, ILogger<SyncOrchestrator> logger, ILoggerFactory loggerFactory)
        {
            _settingsService = settingsService;
            _logger = logger;
            _loggerFactory = loggerFactory;
        }

        /// <summary>
        /// Two-stage sync with parallel pipeline:
        ///   Stage 1 — Download from device to local backup folder (sequential).
        ///   Stage 2 — Upload from local backup folder to Immich (parallel via queue).
        /// Files queued for upload as soon as Stage 1 completes.
        /// Skips files matching exclusion patterns.
        /// </summary>
        public async Task StartSyncAsync(IDeviceProvider device, CancellationToken cancellationToken = default)
        {
            var metrics = new SyncMetrics { StartTime = DateTime.UtcNow };
            try
            {
                var config = _settingsService.Config;

                if (string.IsNullOrWhiteSpace(config.LocalBackupFolder))
                {
                    Progress("Local Backup Folder is not configured. Please set it in Settings.");
                    return;
                }

                // Validate disk space before sync
                if (!ValidateDiskSpace(config.LocalBackupFolder))
                {
                    return;
                }

                Progress($"Connecting to {device.DeviceName}...");
                await device.ConnectAsync(cancellationToken);

                if (!device.IsConnected)
                {
                    Progress("Failed to connect to device.");
                    return;
                }

                Progress("Scanning for files...");
                var files = await device.GetFilesAsync(cancellationToken);
                Progress($"Scan complete. Found {files.Count} file(s).");

                // Apply exclusion filters
                var fileFilter = new FileFilter(config.ExclusionPatterns);
                var filteredFiles = files.Where(f => !fileFilter.ShouldExclude(f.FileName)).ToList();
                var skippedCount = files.Count - filteredFiles.Count;
                if (skippedCount > 0)
                    Progress($"Excluded {skippedCount} file(s) based on filters.");

                var state = _settingsService.State;
                if (!state.SyncedFiles.ContainsKey(device.DeviceId))
                    state.SyncedFiles[device.DeviceId] = new HashSet<string>();
                var syncedSet = state.SyncedFiles[device.DeviceId];

                var localSync = new LocalFolderSync(
                    config.LocalBackupFolder,
                    CreateLogger<LocalFolderSync>());

                metrics.TotalFiles = filteredFiles.Count;
                OnMetricsUpdated?.Invoke(metrics);

                // Parallel pipeline: queue for uploads
                var uploadQueue = new BlockingCollection<SyncFileJob>(boundedCapacity: 5);
                int totalFiles = filteredFiles.Count;
                int processedFiles = 0;
                int uploadedFiles = 0;
                int failedFiles = 0;

                // Stage 2 uploader task (runs concurrently with Stage 1 downloads)
                var uploadTask = Task.Run(async () =>
                {
                    try
                    {
                        foreach (var job in uploadQueue.GetConsumingEnumerable(cancellationToken))
                        {
                            if (!job.DownloadSuccess) 
                            {
                                Interlocked.Increment(ref failedFiles);
                                metrics.FailedFiles = failedFiles;
                                continue;
                            }

                            bool immichOk = true;
                            if (config.EnableImmichSync &&
                                !string.IsNullOrEmpty(config.ImmichUrl) &&
                                !string.IsNullOrEmpty(config.ImmichApiKey))
                            {
                                Progress($"  → [Upload] {job.File.FileName}");
                                var immichSync = new ImmichSync(config.ImmichUrl, config.ImmichApiKey, device.DeviceId, CreateLogger<ImmichSync>());

                                // Get album name from device config if available
                                var deviceConfig = config.RegisteredDevices.FirstOrDefault(d => d.Id == device.DeviceId);
                                string? albumName = deviceConfig?.ImmichAlbum;

                                immichOk = await immichSync.UploadAsync(job.File, job.LocalPath, albumName, cancellationToken);
                                if (!immichOk) Progress($"  ✗ Immich upload failed: {job.File.FileName}");
                            }

                            if (immichOk)
                            {
                                Interlocked.Increment(ref uploadedFiles);
                                metrics.UploadedFiles = uploadedFiles;
                                metrics.BytesUploaded += job.File.Size;
                                metrics.LastUpdateTime = DateTime.UtcNow;
                                OnMetricsUpdated?.Invoke(metrics);
                                syncedSet.Add(job.File.GetIdentifier());
                                _settingsService.SaveState();
                            }
                            else
                            {
                                Interlocked.Increment(ref failedFiles);
                                metrics.FailedFiles = failedFiles;
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected on cancellation
                    }
                });

                // Stage 1: Download files
                try
                {
                    foreach (var file in filteredFiles)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string fileId = file.GetIdentifier();
                        if (syncedSet.Contains(fileId)) continue;

                        Progress($"[{++processedFiles}/{totalFiles}] Downloading: {file.FileName}");

                        string localPath = localSync.GetLocalBackupPath(file);
                        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

                        bool downloadOk = false;
                        if (!File.Exists(localPath) || new FileInfo(localPath).Length != file.Size)
                        {
                            string tempPath = localPath + ".tmp";
                            try
                            {
                                using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                                {
                                    Stream targetStream = config.DownloadSpeedLimitBps > 0 
                                        ? new ThrottledStream(fs, config.DownloadSpeedLimitBps)
                                        : fs;

                                    try
                                    {
                                        await device.DownloadToStreamAsync(file, targetStream, cancellationToken);
                                    }
                                    finally
                                    {
                                        if (targetStream != fs)
                                            targetStream.Dispose();
                                    }
                                }

                                var tempInfo = new FileInfo(tempPath);
                                if (tempInfo.Length == file.Size)
                                {
                                    if (File.Exists(localPath))
                                        File.Delete(localPath);
                                    File.Move(tempPath, localPath, overwrite: true);
                                    downloadOk = true;
                                    metrics.DownloadedFiles++;
                                    metrics.BytesDownloaded += file.Size;
                                    metrics.LastUpdateTime = DateTime.UtcNow;
                                    OnMetricsUpdated?.Invoke(metrics);
                                    _logger.LogDebug("Downloaded {File} ({Size} bytes)", file.FileName, file.Size);
                                }
                                else
                                {
                                    File.Delete(tempPath);
                                    Progress($"  ✗ Incomplete: {file.FileName} ({tempInfo.Length}/{file.Size}B)");
                                    Interlocked.Increment(ref failedFiles);
                                    metrics.FailedFiles = failedFiles;
                                }
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                if (File.Exists(tempPath))
                                    File.Delete(tempPath);
                                Progress($"  ✗ Download failed: {file.FileName}");
                                Interlocked.Increment(ref failedFiles);
                                metrics.FailedFiles = failedFiles;
                            }
                        }
                        else
                        {
                            downloadOk = true; // File already exists with correct size
                            metrics.DownloadedFiles++;
                            metrics.BytesDownloaded += file.Size;
                            metrics.LastUpdateTime = DateTime.UtcNow;
                            OnMetricsUpdated?.Invoke(metrics);
                        }

                        // Queue for parallel upload
                        if (downloadOk)
                        {
                            var job = new SyncFileJob { File = file, LocalPath = localPath, DownloadSuccess = true };
                            uploadQueue.TryAdd(job, 5000, cancellationToken); // 5s timeout
                        }

                        // Optional cleanup after successful download
                        if (downloadOk && config.DeleteAfterSync && config.KeepFilesDays == 0)
                        {
                            try
                            {
                                await device.DeleteFileAsync(file, cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Could not delete file from device: {File}", file.FileName);
                            }
                        }
                    }
                }
                finally
                {
                    uploadQueue.CompleteAdding();
                }

                // Wait for all uploads to complete
                await uploadTask;

                // Cleanup old files if needed
                if (config.DeleteAfterSync && config.KeepFilesDays > 0)
                {
                    Progress("Cleaning up old files from device...");
                    var cutoff = DateTime.Now.AddDays(-config.KeepFilesDays);
                    foreach (var file in filteredFiles.Where(f => f.CreationTime < cutoff && syncedSet.Contains(f.GetIdentifier())))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await device.DeleteFileAsync(file, cancellationToken);
                    }
                }

                device.Disconnect();
                Progress($"✓ Sync complete. {processedFiles} file(s) processed.");
                OnSyncCompleted?.Invoke(device.DeviceId);
            }
            catch (OperationCanceledException)
            {
                Progress("Sync cancelled.");
                device.Disconnect();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync error for device {Device}", device.DeviceName);
                Progress($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Scans the local backup folder and uploads any files not yet in Immich.
        /// Useful after a failed upload or for manual re-sync.
        /// </summary>
        public async Task SyncLocalToImmichAsync(CancellationToken cancellationToken = default)
        {
            var config = _settingsService.Config;

            if (string.IsNullOrWhiteSpace(config.LocalBackupFolder))
            {
                Progress("Local Backup Folder is not configured.");
                return;
            }

            if (!config.EnableImmichSync || string.IsNullOrEmpty(config.ImmichUrl) || string.IsNullOrEmpty(config.ImmichApiKey))
            {
                Progress("Immich is not configured. Please set it up in Settings.");
                return;
            }

            const string sourceId = "__local_backup__";
            var state = _settingsService.State;
            if (!state.SyncedFiles.ContainsKey(sourceId))
                state.SyncedFiles[sourceId] = new HashSet<string>();
            var syncedSet = state.SyncedFiles[sourceId];

            var immichSync = new ImmichSync(config.ImmichUrl, config.ImmichApiKey, "local-backup", CreateLogger<ImmichSync>());

            var supportedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".jpg", ".jpeg", ".cr2", ".cr3", ".nef", ".arw", ".dng", ".mp4", ".mov", ".avi" };

            var localFiles = Directory.EnumerateFiles(config.LocalBackupFolder, "*.*", SearchOption.AllDirectories)
                .Where(f => supportedExts.Contains(Path.GetExtension(f)))
                .ToList();

            Progress($"Found {localFiles.Count} file(s) in local backup. Uploading to Immich...");
            int count = 0;

            foreach (var localPath in localFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string fileId = localPath.ToUpperInvariant();
                if (syncedSet.Contains(fileId)) continue;

                var info = new FileInfo(localPath);
                var syncFile = new SyncFile
                {
                    OriginalPath = localPath,
                    FileName = info.Name,
                    Size = info.Length,
                    CreationTime = info.LastWriteTime,
                    RelativePath = info.Name
                };

                Progress($"[{++count}] → Immich: {info.Name}");
                bool ok = await immichSync.UploadAsync(syncFile, localPath, cancellationToken);
                if (ok)
                {
                    syncedSet.Add(fileId);
                    _settingsService.SaveState();
                }
                else
                {
                    Progress($"  ✗ Failed: {info.Name}");
                }
            }

            Progress($"✓ Local→Immich sync complete. {count} file(s) uploaded.");
        }

        private void Progress(string msg) => OnSyncProgress?.Invoke(msg);

        private bool ValidateDiskSpace(string targetPath)
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(targetPath)!);
                const long minRequiredBytes = 100 * 1024 * 1024; // 100 MB minimum

                if (drive.AvailableFreeSpace < minRequiredBytes)
                {
                    Progress($"⚠ Insufficient disk space. Need {FormatBytes(minRequiredBytes)}, Have {FormatBytes(drive.AvailableFreeSpace)}. Sync cancelled.");
                    return false;
                }

                _logger.LogInformation("Disk space available: {Available} MB", drive.AvailableFreeSpace / (1024 * 1024));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not validate disk space at {Path}", targetPath);
                return true; // Allow sync to proceed if check fails
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();
    }
}
