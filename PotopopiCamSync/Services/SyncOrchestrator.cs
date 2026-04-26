using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.Services
{
    public class SyncOrchestrator
    {
        private readonly SettingsService _settingsService;
        public event Action<string> OnSyncProgress;
        public event Action<string> OnSyncCompleted;

        public SyncOrchestrator(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public async Task StartSyncAsync(IDeviceProvider device)
        {
            try
            {
                OnSyncProgress?.Invoke($"Connecting to {device.DeviceName}...");
                await device.ConnectAsync();

                if (!device.IsConnected)
                {
                    OnSyncProgress?.Invoke("Failed to connect to device.");
                    return;
                }

                OnSyncProgress?.Invoke("Scanning for files...");
                var files = await device.GetFilesAsync();
                OnSyncProgress?.Invoke($"Scan complete. Found {files.Count} files.");
                
                var config = _settingsService.Config;
                var state = _settingsService.State;

                if (!state.SyncedFiles.ContainsKey(device.DeviceId))
                {
                    state.SyncedFiles[device.DeviceId] = new HashSet<string>();
                }
                var syncedSet = state.SyncedFiles[device.DeviceId];

                var destinations = new List<ISyncDestination>();
                
                if (!string.IsNullOrEmpty(config.LocalBackupFolder))
                {
                    destinations.Add(new LocalFolderSync(config.LocalBackupFolder));
                }

                if (config.EnableImmichSync && !string.IsNullOrEmpty(config.ImmichUrl) && !string.IsNullOrEmpty(config.ImmichApiKey))
                {
                    destinations.Add(new ImmichSync(config.ImmichUrl, config.ImmichApiKey, device.DeviceId));
                }

                if (destinations.Count == 0)
                {
                    OnSyncProgress?.Invoke("No sync destinations configured.");
                    device.Disconnect();
                    return;
                }

                int total = files.Count;
                int count = 0;
                int syncedCount = 0;

                foreach (var file in files)
                {
                    count++;
                    string fileId = file.GetIdentifier();

                    if (syncedSet.Contains(fileId))
                    {
                        continue; // Already synced
                    }

                    OnSyncProgress?.Invoke($"Syncing {count}/{total}: {file.FileName}...");

                    using (var stream = await device.GetFileStreamAsync(file))
                    {
                        bool allSuccess = true;
                        foreach (var dest in destinations)
                        {
                            stream.Position = 0;
                            bool success = await dest.UploadAsync(file, stream);
                            if (!success)
                            {
                                allSuccess = false;
                                // We might want to log which destination failed
                            }
                        }

                        if (allSuccess)
                        {
                            syncedSet.Add(fileId);
                            syncedCount++;
                            _settingsService.SaveState(); // Save state incrementally or batch it

                            // Handle cleanup
                            if (config.DeleteAfterSync && config.KeepFilesDays == 0)
                            {
                                await device.DeleteFileAsync(file);
                            }
                        }
                    }
                }

                // If KeepFilesDays > 0, we'd do a pass to delete older files
                if (config.DeleteAfterSync && config.KeepFilesDays > 0)
                {
                    OnSyncProgress?.Invoke("Cleaning up old files...");
                    var cutoffDate = DateTime.Now.AddDays(-config.KeepFilesDays);
                    foreach (var file in files)
                    {
                        if (file.CreationTime < cutoffDate && syncedSet.Contains(file.GetIdentifier()))
                        {
                            await device.DeleteFileAsync(file);
                        }
                    }
                }

                device.Disconnect();
                OnSyncProgress?.Invoke($"Sync completed. {syncedCount} new files synced.");
                OnSyncCompleted?.Invoke(device.DeviceId);
            }
            catch (Exception ex)
            {
                OnSyncProgress?.Invoke($"Error: {ex.Message}");
            }
        }
    }
}
