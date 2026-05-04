using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PotopopiCamSync.Services;
using PotopopiCamSync.Repositories;
using PotopopiCamSync.Views;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly SyncOrchestratorService _orchestrator;
        private readonly DeviceMonitorService _deviceMonitor;
        private readonly ISettingsRepository _settingsRepository;
        private readonly ILogger<MainViewModel> _logger;

        private CancellationTokenSource? _syncCts;

        [ObservableProperty]
        private string _statusText = "Ready";

        [ObservableProperty]
        private bool _isSyncing;

        [ObservableProperty]
        private double _syncProgressPercentage;

        [ObservableProperty]
        private bool _immichConfigured;

        [ObservableProperty]
        private bool _showAnalysisResults;

        [ObservableProperty]
        private int _blurryCount;

        [ObservableProperty]
        private int _duplicateCount;

        [ObservableProperty]
        private int _totalFound;

        public ObservableCollection<string> Logs { get; } = new();
        public ObservableCollection<IDeviceProvider> UnregisteredDevices { get; } = new();
        public ObservableCollection<IDeviceProvider> ActiveDevices { get; } = new();
        public ObservableCollection<FlaggedFileModel> FlaggedFiles { get; } = new();

        public MainViewModel(SyncOrchestratorService orchestrator, DeviceMonitorService deviceMonitor, ISettingsRepository settingsRepository, ILogger<MainViewModel> logger)
        {
            _orchestrator = orchestrator;
            _deviceMonitor = deviceMonitor;
            _settingsRepository = settingsRepository;
            _logger = logger;

            _orchestrator.OnSyncProgress += OnSyncProgress;
            _orchestrator.OnSyncCompleted += OnSyncCompleted;
            _orchestrator.OnMetricsUpdated += OnMetricsUpdated;
            _orchestrator.OnAIResultFound += OnAIResultFound;
            _deviceMonitor.OnDeviceConnected += OnDeviceConnected;

            _deviceMonitor.Start();
            RefreshImmichStatus();

            foreach (var log in _settingsRepository.State.PersistentLogs)
            {
                Logs.Add(log);
            }

            foreach (var file in _settingsRepository.State.PersistentFlaggedFiles)
            {
                FlaggedFiles.Add(file);
            }
            if (FlaggedFiles.Count > 0)
                ShowAnalysisResults = true;

            Log($"Restored {Logs.Count} log(s) and {FlaggedFiles.Count} AI result(s) from previous session.");
        }

        public void RefreshImmichStatus()
        {
            var c = _settingsRepository.Config;
            ImmichConfigured = c.EnableImmichSync && (!string.IsNullOrEmpty(c.ImmichUrl) || c.ImmichAccounts.Any());
        }

        [RelayCommand]
        private void ManualSync(IDeviceProvider device)
        {
            if (device == null || IsSyncing) return;
            StartSync(() => _orchestrator.StartSyncAsync(device, GetNewToken(), forceVerify: false));
        }

        [RelayCommand]
        private void ForceSync(IDeviceProvider device)
        {
            if (device == null || IsSyncing) return;
            StartSync(() => _orchestrator.StartSyncAsync(device, GetNewToken(), forceVerify: true));
        }

        [RelayCommand]
        private void RegisterDevice(IDeviceProvider device)
        {
            if (device == null) return;
            var signature = new DeviceSignatureModel { Id = device.DeviceId, Name = device.DeviceName, Type = device is MtpDeviceProvider ? "Mtp" : "SdCard" };
            _settingsRepository.Config.RegisteredDevices.Add(signature);
            _settingsRepository.SaveConfig();
            UnregisteredDevices.Remove(device);
            ActiveDevices.Add(device);
            Log($"Registered: {device.DeviceName}. Starting sync...");
            ManualSync(device);
        }

        [RelayCommand]
        private void SyncLocalToImmich()
        {
            if (IsSyncing) return;
            StartSync(async () => 
            {
                // First, handle local deletions from AI Review
                var toDelete = FlaggedFiles.Where(f => f.IsPendingDeletion).ToList();
                int deletedCount = 0;
                foreach (var file in toDelete)
                {
                    try
                    {
                        if (File.Exists(file.Path)) File.Delete(file.Path);
                        System.Windows.Application.Current.Dispatcher.Invoke(() => 
                        {
                            FlaggedFiles.Remove(file);
                            _settingsRepository.State.PersistentFlaggedFiles.Remove(file);
                        });
                        deletedCount++;
                    }
                    catch (Exception ex) { Log($"Failed to delete {file.FileName}: {ex.Message}"); }
                }
                if (deletedCount > 0) Log($"Deleted {deletedCount} file(s) locally as requested.");

                // Then run the sync
                await _orchestrator.SyncLocalToImmichAsync(GetNewToken());
                
                // After sync, remove successfully uploaded files from FlaggedFiles
                var state = _settingsRepository.State;
                var sourceId = "__local_backup__";
                if (state.SyncedFiles.TryGetValue(sourceId, out var syncedSet))
                {
                    var uploadedFlagged = FlaggedFiles.Where(f => syncedSet.Contains(f.Path.ToUpperInvariant())).ToList();
                    foreach (var file in uploadedFlagged)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() => 
                        {
                            FlaggedFiles.Remove(file);
                            _settingsRepository.State.PersistentFlaggedFiles.Remove(file);
                        });
                    }
                }
                _settingsRepository.SaveState();

                System.Windows.Application.Current.Dispatcher.Invoke(() => ClearCacheAndLogs());
            });
        }

        [RelayCommand]
        private void ClearCacheAndLogs()
        {
            Logs.Clear();
            _settingsRepository.State.PersistentLogs.Clear();
            _settingsRepository.SaveState();
            Log("Cache and Logs cleared.");
        }

        [RelayCommand]
        private void OpenLocalFolder()
        {
            var folder = _settingsRepository.Config.LocalBackupFolder;
            if (string.IsNullOrWhiteSpace(folder)) { Log("Local Folder not set."); return; }
            if (!Directory.Exists(folder)) { Log("Folder not found."); return; }
            try { System.Diagnostics.Process.Start("explorer.exe", folder); }
            catch (Exception ex) { Log($"Explorer error: {ex.Message}"); }
        }



        [RelayCommand]
        private void AllowImmichSync(FlaggedFileModel file)
        {
            if (file == null) return;
            
            var state = _settingsRepository.State;
            if (file.IsAllowed)
            {
                state.AllowedAIRejectedFiles.Remove(file.Path);
                file.IsAllowed = false;
                Log($"Revoked {file.FileName} from whitelist.");
            }
            else
            {
                state.AllowedAIRejectedFiles.Add(file.Path);
                file.IsAllowed = true;
                Log($"Whitelisted {file.FileName} for Immich sync.");
            }
            _settingsRepository.SaveState();
        }

        [RelayCommand]
        private void AllowSelectedImmichSync()
        {
            var selectedFiles = FlaggedFiles.Where(f => f.IsSelected).ToList();
            if (selectedFiles.Count == 0) return;

            var state = _settingsRepository.State;
            int count = 0;
            foreach (var file in selectedFiles)
            {
                state.AllowedAIRejectedFiles.Add(file.Path);
                file.IsAllowed = true;
                count++;
            }
            _settingsRepository.SaveState();
            
            Log($"Whitelisted {count} file(s) for Immich sync.");
        }

        [RelayCommand]
        private void RevokeSelectedImmichSync()
        {
            var selectedFiles = FlaggedFiles.Where(f => f.IsSelected).ToList();
            if (selectedFiles.Count == 0) return;

            var state = _settingsRepository.State;
            int count = 0;
            foreach (var file in selectedFiles)
            {
                state.AllowedAIRejectedFiles.Remove(file.Path);
                file.IsAllowed = false;
                count++;
            }
            _settingsRepository.SaveState();
            
            Log($"Revoked {count} file(s) from whitelist.");
        }

        [RelayCommand]
        private void ToggleDeleteLocal(FlaggedFileModel file)
        {
            if (file == null) return;
            file.IsPendingDeletion = !file.IsPendingDeletion;
            _settingsRepository.SaveState();
            Log(file.IsPendingDeletion ? $"Marked {file.FileName} for deletion." : $"Unmarked {file.FileName} for deletion.");
        }

        [RelayCommand]
        private void DeleteSelectedLocal()
        {
            var selectedFiles = FlaggedFiles.Where(f => f.IsSelected).ToList();
            if (selectedFiles.Count == 0) return;

            int count = 0;
            foreach (var file in selectedFiles)
            {
                file.IsPendingDeletion = true;
                count++;
            }
            _settingsRepository.SaveState();
            Log($"Marked {count} file(s) for deletion.");
        }

        [RelayCommand]
        private void ToggleSelectAll(bool? select)
        {
            if (select == null) return;
            foreach (var file in FlaggedFiles)
            {
                file.IsSelected = select.Value;
            }
        }

        [RelayCommand]
        private void CancelSync() => _syncCts?.Cancel();

        private void StartSync(Func<Task> syncAction)
        {
            SyncProgressPercentage = 0;
            IsSyncing = true;
            Task.Run(async () => 
            {
                try 
                {
                    await syncAction();
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => Log($"Sync error: {ex.InnerException?.Message ?? ex.Message}"));
                }
                finally
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => IsSyncing = false);
                }
            });
        }

        private CancellationToken GetNewToken()
        {
            _syncCts?.Cancel();
            _syncCts = new CancellationTokenSource();
            return _syncCts.Token;
        }

        private void OnDeviceConnected(IDeviceProvider device)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => {
                bool isRegistered = _settingsRepository.Config.RegisteredDevices.Any(d => d.Id == device.DeviceId);
                if (isRegistered)
                {
                    if (!ActiveDevices.Any(d => d.DeviceId == device.DeviceId)) ActiveDevices.Add(device);
                    Log($"Registered device {device.DeviceName} detected. Auto-syncing...");
                    ManualSync(device);
                }
                else
                {
                    if (!UnregisteredDevices.Any(d => d.DeviceId == device.DeviceId)) UnregisteredDevices.Add(device);
                }
            });
        }

        private void OnAIResultFound(string path, string reason, bool isBlurry)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => {
                var model = new FlaggedFileModel { Path = path, Reason = reason, IsBlurry = isBlurry };
                if (!FlaggedFiles.Any(f => f.Path == path))
                {
                    FlaggedFiles.Add(model);
                    _settingsRepository.State.PersistentFlaggedFiles.Add(model);
                    _settingsRepository.SaveState();
                }
            });
        }

        private void OnMetricsUpdated(SyncMetricsModel metrics)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => {
                if (metrics.TotalFiles > 0)
                {
                    double total = metrics.TotalFiles * 2;
                    double done = metrics.DownloadedFiles + metrics.UploadedFiles;
                    SyncProgressPercentage = (done / total) * 100;
                    TotalFound = metrics.TotalFiles;
                    BlurryCount = metrics.BlurryFiles;
                    DuplicateCount = metrics.DuplicateFiles;
                    
                    if (BlurryCount > 0 || DuplicateCount > 0)
                        ShowAnalysisResults = true;
                }
            });
        }

        private void OnSyncProgress(string message)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => {
                StatusText = message;
                Log(message);
            });
        }

        private void OnSyncCompleted(IDeviceProvider device)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => {
                IsSyncing = false;
                StatusText = "Done. Ready for next device.";
                Log($"Sync completed: {device.DisplayName}");
                App.ShowTrayNotification("Sync Complete", $"Successfully synced device {device.DisplayName}");
            });
        }

        private void Log(string message)
        {
            string entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            Logs.Insert(0, entry);
            if (Logs.Count > 100) Logs.RemoveAt(100);

            var state = _settingsRepository.State;
            state.PersistentLogs.Insert(0, entry);
            if (state.PersistentLogs.Count > 100) state.PersistentLogs.RemoveAt(100);
            _settingsRepository.SaveState();

            _logger.LogInformation(message);
        }
    }
}
