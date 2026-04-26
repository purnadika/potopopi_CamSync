using System;
using System.Collections.Generic;
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

        public event Action<string>? OnSyncProgress;
        public event Action<string>? OnSyncCompleted;

        public SyncOrchestrator(SettingsService settingsService, ILogger<SyncOrchestrator> logger)
        {
            _settingsService = settingsService;
            _logger = logger;
        }

        /// <summary>
        /// Two-stage sync:
        ///   Stage 1 — Download from device to local backup folder (always required).
        ///   Stage 2 — Upload from local backup folder to Immich (if configured).
        /// </summary>
        public async Task StartSyncAsync(IDeviceProvider device, CancellationToken cancellationToken = default)
        {
            try
            {
                var config = _settingsService.Config;

                if (string.IsNullOrWhiteSpace(config.LocalBackupFolder))
                {
                    Progress("Local Backup Folder is not configured. Please set it in Settings.");
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

                var state = _settingsService.State;
                if (!state.SyncedFiles.ContainsKey(device.DeviceId))
                    state.SyncedFiles[device.DeviceId] = new HashSet<string>();
                var syncedSet = state.SyncedFiles[device.DeviceId];

                var localSync = new LocalFolderSync(
                    config.LocalBackupFolder,
                    CreateLogger<LocalFolderSync>());

                int total = files.Count;
                int newCount = 0;

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string fileId = file.GetIdentifier();
                    if (syncedSet.Contains(fileId)) continue;

                    Progress($"[{++newCount}/{total}] Downloading: {file.FileName}");

                    // ── Stage 1: Download to local backup ──────────────────────────
                    string localPath = localSync.GetLocalBackupPath(file);
                    Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

                    // Only download if not already on disk
                    if (!File.Exists(localPath) || new FileInfo(localPath).Length != file.Size)
                    {
                        using (var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                        {
                            await device.DownloadToStreamAsync(file, fs, cancellationToken);
                        }
                    }

                    // ── Stage 2: Upload from local path to Immich ───────────────────
                    bool immichOk = true;
                    if (config.EnableImmichSync &&
                        !string.IsNullOrEmpty(config.ImmichUrl) &&
                        !string.IsNullOrEmpty(config.ImmichApiKey))
                    {
                        Progress($"  → Uploading to Immich: {file.FileName}");
                        var immichSync = new ImmichSync(config.ImmichUrl, config.ImmichApiKey, device.DeviceId, CreateLogger<ImmichSync>());
                        immichOk = await immichSync.UploadAsync(file, localPath, cancellationToken);
                        if (!immichOk) Progress($"  ✗ Immich upload failed for {file.FileName}");
                    }

                    if (immichOk)
                    {
                        syncedSet.Add(fileId);
                        _settingsService.SaveState();
                    }

                    // ── Optional cleanup ────────────────────────────────────────────
                    if (config.DeleteAfterSync && config.KeepFilesDays == 0 && immichOk)
                    {
                        await device.DeleteFileAsync(file, cancellationToken);
                    }
                }

                // Cleanup files older than KeepFilesDays from device
                if (config.DeleteAfterSync && config.KeepFilesDays > 0)
                {
                    Progress("Cleaning up old files from device...");
                    var cutoff = DateTime.Now.AddDays(-config.KeepFilesDays);
                    foreach (var file in files.Where(f => f.CreationTime < cutoff && syncedSet.Contains(f.GetIdentifier())))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await device.DeleteFileAsync(file, cancellationToken);
                    }
                }

                device.Disconnect();
                Progress($"✓ Sync complete. {newCount} new file(s) synced.");
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

        private ILogger<T> CreateLogger<T>() =>
            Microsoft.Extensions.Logging.Abstractions.NullLogger<T>.Instance;
    }
}
