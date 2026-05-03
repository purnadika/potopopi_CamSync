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
        private readonly SyncOrchestrator _orchestrator;
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
        public ObservableCollection<FlaggedFile> FlaggedFiles { get; } = new();

        public MainViewModel(SyncOrchestrator orchestrator, DeviceMonitorService deviceMonitor, ISettingsRepository settingsRepository, ILogger<MainViewModel> logger)
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
            ShowAnalysisResults = false;
            FlaggedFiles.Clear();
            StartSync(() => _orchestrator.StartSyncAsync(device, GetNewToken()));
        }

        [RelayCommand]
        private void RegisterDevice(IDeviceProvider device)
        {
            if (device == null) return;
            var signature = new DeviceSignature { Id = device.DeviceId, Name = device.DeviceName, Type = device is MtpDeviceProvider ? "Mtp" : "SdCard" };
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
            StartSync(() => _orchestrator.SyncLocalToImmichAsync(GetNewToken()));
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
        private void ReviewAIResults()
        {
            var window = new AIReviewWindow { DataContext = this };
            window.ShowDialog();
        }

        [RelayCommand]
        private void CancelSync() => _syncCts?.Cancel();

        private void StartSync(Func<Task> syncAction)
        {
            SyncProgressPercentage = 0;
            IsSyncing = true;
            _ = syncAction().ContinueWith(t => {
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    IsSyncing = false;
                    if (t.IsFaulted) Log($"Sync error: {t.Exception?.InnerException?.Message}");
                });
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
                FlaggedFiles.Add(new FlaggedFile { Path = path, Reason = reason, IsBlurry = isBlurry });
            });
        }

        private void OnMetricsUpdated(SyncMetrics metrics)
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

        private void OnSyncCompleted(string deviceId)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => {
                IsSyncing = false;
                StatusText = "Done. Ready for next device.";
                Log($"Sync completed: {deviceId}");
                App.ShowTrayNotification("Sync Complete", $"Successfully synced device {deviceId}");
            });
        }

        private void Log(string message)
        {
            string entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            Logs.Insert(0, entry);
            if (Logs.Count > 100) Logs.RemoveAt(100);
            _logger.LogInformation(message);
        }
    }

    public class FlaggedFile
    {
        public string Path { get; set; } = string.Empty;
        public string FileName => System.IO.Path.GetFileName(Path);
        public string Reason { get; set; } = string.Empty;
        public bool IsBlurry { get; set; }
    }
}
