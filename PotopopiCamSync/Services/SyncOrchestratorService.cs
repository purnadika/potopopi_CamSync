using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PotopopiCamSync.Models;
using PotopopiCamSync.Repositories;
using PotopopiCamSync.Utilities;

namespace PotopopiCamSync.Services
{
    public class SyncOptions
    {
        public bool EnableImmich { get; set; }
        public bool EnableGoogleDrive { get; set; }
        public bool EnableOneDrive { get; set; }
    }

    public class SyncOrchestratorService
    {
        private readonly ISettingsRepository _settingsRepository;
        private readonly IMediaAnalyzer _mediaAnalyzer;
        private readonly ILogger<SyncOrchestratorService> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ITokenStorageService _tokenStorage;

        // Untuk parallel pipeline
        private class SyncFileJob
        {
            public SyncFileModel File { get; set; } = null!;
            public string LocalPath { get; set; } = null!;
            public bool DownloadSuccess { get; set; }
        }

        public event Action<string>? OnSyncProgress;
        public event Action<IDeviceProvider>? OnSyncCompleted;
        public event Action<SyncMetricsModel>? OnMetricsUpdated;
        public event Action<string, string, bool>? OnAIResultFound; // LocalPath, Reason, IsBlurry


        public SyncOrchestratorService(ISettingsRepository settingsRepository, IMediaAnalyzer mediaAnalyzer, ILogger<SyncOrchestratorService> logger, ILoggerFactory loggerFactory, ITokenStorageService tokenStorage)
        {
            _settingsRepository = settingsRepository;
            _mediaAnalyzer = mediaAnalyzer;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _tokenStorage = tokenStorage;
        }

        public async Task StartSyncAsync(IDeviceProvider device, SyncOptions options, CancellationToken cancellationToken = default, bool forceVerify = false)
        {
            var metrics = new SyncMetricsModel { StartTime = DateTime.UtcNow };
            try
            {
                var config = _settingsRepository.Config;

                if (string.IsNullOrWhiteSpace(config.LocalBackupFolder))
                {
                    Progress(MessageConstants.General.LocalBackupFolderNotSet);
                    return;
                }

                if (!ValidateDiskSpace(config.LocalBackupFolder)) return;

                Progress(string.Format(MessageConstants.Sync.ConnectingToDevice, device.DeviceName));
                await device.ConnectAsync(cancellationToken);

                if (!device.IsConnected)
                {
                    Progress(MessageConstants.General.FailedToConnect);
                    return;
                }

                Progress(MessageConstants.General.ScanningFiles);
                var allFiles = await device.GetFilesAsync(cancellationToken, msg => Progress(msg));

                
                var fileFilter = new FileFilter(config.ExclusionPatterns);
                var filteredFiles = allFiles.Where(f => !fileFilter.ShouldExclude(f.FileName)).ToList();

                // Smart Scan
                var deviceConfig = config.RegisteredDevices.FirstOrDefault(d => string.Equals(d.Id, device.DeviceId, StringComparison.OrdinalIgnoreCase));
                if (deviceConfig?.UseSmartScan == true && deviceConfig.LastSyncDate.HasValue)
                {
                    var since = deviceConfig.LastSyncDate.Value.AddHours(-1);
                    Progress(string.Format(MessageConstants.Sync.SmartScanFound, filteredFiles.Count));
                }
                else
                {
                    Progress(string.Format(MessageConstants.Sync.ScanCompleteWithCount, filteredFiles.Count));
                }

                var state = _settingsRepository.State;
                if (!state.SyncedFiles.ContainsKey(device.DeviceId))
                    state.SyncedFiles[device.DeviceId] = new HashSet<string>();
                var syncedSet = state.SyncedFiles[device.DeviceId];

                var localSync = new LocalFolderSyncService(
                    deviceConfig?.LocalFolderOverride ?? config.LocalBackupFolder,
                    CreateLogger<LocalFolderSyncService>());

                metrics.TotalFiles = filteredFiles.Count;
                OnMetricsUpdated?.Invoke(metrics);

                var uploadQueue = new BlockingCollection<SyncFileJob>(boundedCapacity: 5);
                var uploadTask = RunUploadPipelineAsync(device, options, uploadQueue, metrics, syncedSet, cancellationToken);

                try
                {
                    int processedCount = 0;
                    foreach (var file in filteredFiles)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string fileId = file.GetIdentifier();
                        string localPath = localSync.GetLocalBackupPath(file);
                        
                        bool shouldSync = !syncedSet.Contains(fileId);
                        if (!shouldSync && forceVerify)
                        {
                            if (!File.Exists(localPath) || new FileInfo(localPath).Length != file.Size)
                            {
                                shouldSync = true;
                                Progress(string.Format(MessageConstants.Sync.ForceSyncRedownload, file.FileName));
                            }
                        }

                        if (!shouldSync) continue;
                        Progress(string.Format(MessageConstants.Sync.ProgressDownload, ++processedCount, filteredFiles.Count, file.FileName));

                        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

                        bool downloadOk = await EnsureDownloadedAsync(device, file, localPath, metrics, cancellationToken);
                        
                        if (downloadOk)
                        {
                            bool isRejectedByAI = false;
                            
                            // AI Analysis
                            if (config.AIAnalysisMode != AIAnalysisMode.None && FileUtilities.IsImage(file.FileName))
                            {
                                var aiEngine = _mediaAnalyzer;
                                if (aiEngine != null)
                                {
                                    Progress(string.Format(MessageConstants.Sync.AIAnalysisStart, file.FileName));
                                    var analysis = await aiEngine.AnalyzeAsync(localPath, cancellationToken);
                                    if (analysis.IsPotentiallyBlurry)
                                    {
                                        metrics.BlurryFiles++;
                                        Progress(string.Format(MessageConstants.Sync.AIAnalysisBlurry, file.FileName, analysis.BlurScore));
                                        OnAIResultFound?.Invoke(localPath, "Blurry", true);
                                        OnMetricsUpdated?.Invoke(metrics);
                                        isRejectedByAI = true;
                                    }
                                    
                                    // Basic duplicate check if enabled
                                    if (analysis.ImageHash != 0)
                                    {
                                        // TODO: Implement pHash lookup in state
                                    }
                                }
                            }

                            if (isRejectedByAI && !state.AllowedAIRejectedFiles.Contains(localPath))
                            {
                                Progress(MessageConstants.Sync.AISkippingImmich);
                            }
                            else
                            {
                                uploadQueue.TryAdd(new SyncFileJob { File = file, LocalPath = localPath, DownloadSuccess = true }, 5000, cancellationToken);
                            }
                        }
                    }
                }
                finally
                {
                    uploadQueue.CompleteAdding();
                }

                await uploadTask;

                if (deviceConfig != null)
                {
                    deviceConfig.LastSyncDate = DateTime.Now;
                    _settingsRepository.SaveConfig();
                }

                device.Disconnect();
                Progress($"{MessageConstants.General.SyncComplete} {metrics.UploadedFiles} file(s) uploaded to {CloudConstants.Immich.ProviderName}.");
                OnSyncCompleted?.Invoke(device);
            }
            catch (OperationCanceledException) { Progress(MessageConstants.General.SyncCancelled); device.Disconnect(); }
            catch (Exception ex) { _logger.LogError(ex, "Sync error"); Progress($"Error: {ex.Message}"); }
        }

        private async Task<bool> EnsureDownloadedAsync(IDeviceProvider device, SyncFileModel file, string localPath, SyncMetricsModel metrics, CancellationToken ct)
        {
            if (File.Exists(localPath) && new FileInfo(localPath).Length == file.Size)
            {
                metrics.DownloadedFiles++;
                metrics.BytesDownloaded += file.Size;
                OnMetricsUpdated?.Invoke(metrics);
                return true;
            }

            string tempPath = localPath + ".tmp";
            try
            {
                var config = _settingsRepository.Config;
                using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    Stream targetStream = config.DownloadSpeedLimitBps > 0 ? new ThrottledStream(fs, config.DownloadSpeedLimitBps) : fs;
                    try { await device.DownloadToStreamAsync(file, targetStream, ct); }
                    finally { if (targetStream != fs) targetStream.Dispose(); }
                }

                if (new FileInfo(tempPath).Length == file.Size)
                {
                    if (File.Exists(localPath)) File.Delete(localPath);
                    File.Move(tempPath, localPath, overwrite: true);
                    metrics.DownloadedFiles++;
                    metrics.BytesDownloaded += file.Size;
                    OnMetricsUpdated?.Invoke(metrics);
                    return true;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { _logger.LogWarning(ex, "Download failed"); }
            finally { if (File.Exists(tempPath)) File.Delete(tempPath); }
            return false;
        }

        private Task RunUploadPipelineAsync(IDeviceProvider device, SyncOptions options, BlockingCollection<SyncFileJob> queue, SyncMetricsModel metrics, HashSet<string> syncedSet, CancellationToken ct)
        {
            return Task.Run(async () =>
            {
                var config = _settingsRepository.Config;
                // Prepare destinations
                var destinations = new List<ISyncDestination>();
                
                if (options.EnableImmich && config.EnableImmichSync)
                {
                    var deviceId = device.DeviceId;
                    var deviceConfig = config.RegisteredDevices.FirstOrDefault(d => string.Equals(d.Id, deviceId, StringComparison.OrdinalIgnoreCase));
                    var accountId = deviceConfig?.ImmichAccountId;
                    var account = config.ImmichAccounts.FirstOrDefault(a => string.Equals(a.Id, accountId, StringComparison.OrdinalIgnoreCase)) 
                                  ?? new ImmichAccountModel { Url = config.ImmichUrl, ApiKey = config.ImmichApiKey };
                    
                    if (!string.IsNullOrEmpty(account.Url) && !string.IsNullOrEmpty(account.ApiKey))
                    {
                        destinations.Add(new ImmichSyncService(account.Url, account.ApiKey, device.DeviceId, CreateLogger<ImmichSyncService>()));
                    }
                }

                // Add Google Drive if enabled
                if (options.EnableGoogleDrive && config.GoogleDriveAccount.IsEnabled && config.GoogleDriveAccount.IsAuthenticated)
                {
                    destinations.Add(new GoogleDriveSyncService(_loggerFactory.CreateLogger<GoogleDriveSyncService>(), config, _tokenStorage));
                }

                // Add OneDrive if enabled
                if (options.EnableOneDrive && config.OneDriveAccount.IsEnabled && config.OneDriveAccount.IsAuthenticated)
                {
                    destinations.Add(new OneDriveSyncService(_loggerFactory.CreateLogger<OneDriveSyncService>(), config, _tokenStorage));
                }

                int uploadedFiles = 0;
                int failedFiles = 0;

                try
                {
                    foreach (var job in queue.GetConsumingEnumerable(ct))
                    {
                        if (ct.IsCancellationRequested) break;

                        bool allOk = true;
                        foreach (var destination in destinations)
                        {
                            if (!destination.IsEnabled) continue;

                            Progress(string.Format(MessageConstants.Sync.UploadingToDestination, destination.Name, job.File.FileName));
                            bool ok = await destination.UploadAsync(job.File, job.LocalPath, ct);
                            if (!ok)
                            {
                                Progress(string.Format(MessageConstants.Sync.UploadFailed, destination.Name, job.File.FileName));
                                allOk = false;
                            }
                        }

                        if (allOk)
                        {
                            metrics.UploadedFiles = Interlocked.Increment(ref uploadedFiles);
                            metrics.BytesUploaded += job.File.Size;
                            syncedSet.Add(job.File.GetIdentifier());
                            _settingsRepository.SaveState();
                            OnMetricsUpdated?.Invoke(metrics);
                        }
                        else
                        {
                            metrics.FailedFiles = Interlocked.Increment(ref failedFiles);
                        }
                    }
                }
                catch (OperationCanceledException) { _logger.LogInformation("Upload pipeline stopped via cancellation."); }
                catch (Exception ex) { _logger.LogError(ex, "Critical error in upload pipeline."); }
            }, ct);
        }



        public async Task SyncLocalToImmichAsync(CancellationToken cancellationToken = default)
        {
            // Update this later if needed
            var config = _settingsRepository.Config;
            if (string.IsNullOrWhiteSpace(config.LocalBackupFolder)) { Progress(MessageConstants.General.LocalBackupFolderNotSet); return; }
            if (!config.EnableImmichSync || string.IsNullOrEmpty(config.ImmichUrl) || string.IsNullOrEmpty(config.ImmichApiKey)) { Progress(MessageConstants.Sync.ImmichNotSet); return; }
            var state = _settingsRepository.State;
            if (!state.SyncedFiles.ContainsKey(AppConstants.Identifiers.LocalBackupSourceId)) state.SyncedFiles[AppConstants.Identifiers.LocalBackupSourceId] = new HashSet<string>();
            var syncedSet = state.SyncedFiles[AppConstants.Identifiers.LocalBackupSourceId];
            var immichSync = new ImmichSyncService(config.ImmichUrl, config.ImmichApiKey, AppConstants.Identifiers.LocalBackupAccountId, CreateLogger<ImmichSyncService>());
            if (!await immichSync.PingAsync(cancellationToken))
            {
                Progress(MessageConstants.General.ImmichInaccessible);
                return;
            }
            
            var localFiles = Directory.EnumerateFiles(config.LocalBackupFolder, "*.*", SearchOption.AllDirectories).Where(f => FileUtilities.IsSupportedMedia(f)).ToList();
            
            var metrics = new SyncMetricsModel 
            { 
                StartTime = DateTime.UtcNow,
                TotalFiles = localFiles.Count,
                DownloadedFiles = localFiles.Count // All files are already local
            };
            OnMetricsUpdated?.Invoke(metrics);

            Progress(string.Format(MessageConstants.Sync.LocalToImmichFound, localFiles.Count, CloudConstants.Immich.ProviderName));
            int count = 0;
            var immichFilter = new FileFilter(config.ImmichExclusionPatterns);
            foreach (var localPath in localFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string fileId = localPath.ToUpperInvariant();
                if (syncedSet.Contains(fileId)) continue;
                var info = new FileInfo(localPath);
                if (immichFilter.ShouldExclude(info.Name)) { syncedSet.Add(fileId); _settingsRepository.SaveState(); continue; }
                
                bool isRejectedByAI = false;
                if (config.AIAnalysisMode != AIAnalysisMode.None && FileUtilities.IsImage(info.Name))
                {
                    var aiEngine = _mediaAnalyzer;
                    if (aiEngine != null)
                    {
                        var analysis = await aiEngine.AnalyzeAsync(localPath, cancellationToken);
                        if (analysis.IsPotentiallyBlurry) isRejectedByAI = true;
                    }
                }

                if (isRejectedByAI && !state.AllowedAIRejectedFiles.Contains(localPath))
                {
                    Progress(string.Format(MessageConstants.Sync.LocalToImmichSkippingAI, info.Name));
                    continue;
                }

                var syncFile = new SyncFileModel { OriginalPath = localPath, FileName = info.Name, Size = info.Length, CreationTime = info.LastWriteTime, RelativePath = info.Name };
                Progress(string.Format(MessageConstants.Sync.LocalToImmichProgress, ++count, CloudConstants.Immich.ProviderName, info.Name));
                bool ok = await immichSync.UploadAsync(syncFile, localPath, null, cancellationToken);
                if (ok) 
                { 
                    syncedSet.Add(fileId); 
                    _settingsRepository.SaveState(); 
                    metrics.UploadedFiles++;
                    metrics.BytesUploaded += info.Length;
                }
                else 
                { 
                    Progress(string.Format(MessageConstants.Sync.LocalToImmichFailed, info.Name)); 
                    metrics.FailedFiles++;
                }
                OnMetricsUpdated?.Invoke(metrics);
            }
            Progress(string.Format(MessageConstants.Sync.LocalToImmichComplete, CloudConstants.Immich.ProviderName));
        }

        private void Progress(string msg) => OnSyncProgress?.Invoke(msg);

        private bool ValidateDiskSpace(string targetPath)
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(targetPath)!);
                const long minRequiredBytes = 100 * 1024 * 1024;
                if (drive.AvailableFreeSpace < minRequiredBytes) { Progress(MessageConstants.General.DiskSpaceInsufficient); return false; }
                return true;
            }
            catch { return true; }
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1) { order++; len = len / 1024; }
            return $"{len:0.##} {sizes[order]}";
        }

        private ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();
    }
}
