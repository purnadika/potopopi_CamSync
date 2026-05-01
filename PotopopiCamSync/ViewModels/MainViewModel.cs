using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PotopopiCamSync.Services;
using PotopopiCamSync.Views;

namespace PotopopiCamSync.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly SyncOrchestrator _orchestrator;
        private readonly DeviceMonitorService _deviceMonitor;
        private readonly SettingsService _settingsService;
        private readonly ILogger<MainViewModel> _logger;

        private CancellationTokenSource? _syncCts;
        private LoadingWindow? _loadingWindow;

        [ObservableProperty]
        private string _statusText = "Waiting for devices...";

        [ObservableProperty]
        private bool _isSyncing;

        [ObservableProperty]
        private double _syncProgressPercentage;

        [ObservableProperty]
        private bool _immichConfigured;

        public ObservableCollection<string> Logs { get; } = new();
        public ObservableCollection<IDeviceProvider> UnregisteredDevices { get; } = new();
        public ObservableCollection<IDeviceProvider> ActiveDevices { get; } = new();

        public MainViewModel(SyncOrchestrator orchestrator, DeviceMonitorService deviceMonitor, SettingsService settingsService, ILogger<MainViewModel> logger)
        {
            _orchestrator = orchestrator;
            _deviceMonitor = deviceMonitor;
            _settingsService = settingsService;
            _logger = logger;

            _orchestrator.OnSyncProgress += OnSyncProgress;
            _orchestrator.OnSyncCompleted += OnSyncCompleted;
            _orchestrator.OnMetricsUpdated += OnMetricsUpdated;
            _deviceMonitor.OnDeviceConnected += OnDeviceConnected;

            _deviceMonitor.Start();

            RefreshImmichStatus();
        }

        public void RefreshImmichStatus()
        {
            var c = _settingsService.Config;
            ImmichConfigured = c.EnableImmichSync &&
                               !string.IsNullOrEmpty(c.ImmichUrl) &&
                               !string.IsNullOrEmpty(c.ImmichApiKey) &&
                               !string.IsNullOrEmpty(c.LocalBackupFolder);
        }

        // ── Commands ────────────────────────────────────────────────────────────

        [RelayCommand]
        private void ManualSync(IDeviceProvider device)
        {
            if (device == null || IsSyncing) return;
            StartSync(() => _orchestrator.StartSyncAsync(device, GetNewToken()));
        }

        [RelayCommand]
        private void RegisterDevice(IDeviceProvider device)
        {
            if (device == null) return;

            var signature = new Models.DeviceSignature
            {
                Id = device.DeviceId,
                Name = device.DeviceName,
                Type = device is MtpDeviceProvider ? "Mtp" : "SdCard"
            };

            _settingsService.Config.RegisteredDevices.Add(signature);
            _settingsService.SaveConfig();

            UnregisteredDevices.Remove(device);
            ActiveDevices.Add(device);
            Log($"Registered device: {device.DeviceName}. Starting sync...");

            StartSync(() => _orchestrator.StartSyncAsync(device, GetNewToken()));
        }

        [RelayCommand]
        private void SyncLocalToImmich()
        {
            if (IsSyncing) return;
            StartSync(() => _orchestrator.SyncLocalToImmichAsync(GetNewToken()));
        }

        [RelayCommand]
        private void CancelSync()
        {
            _syncCts?.Cancel();
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private void StartSync(Func<System.Threading.Tasks.Task> syncAction)
        {
            SyncProgressPercentage = 0;
            IsSyncing = true;
            _ = syncAction().ContinueWith(_ =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => IsSyncing = false);
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
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                bool isRegistered = _settingsService.Config.RegisteredDevices.Exists(d =>
                    string.Equals(d.Id, device.DeviceId, StringComparison.OrdinalIgnoreCase));

                if (isRegistered)
                {
                    bool exists = false;
                    foreach (var d in ActiveDevices)
                        if (string.Equals(d.DeviceId, device.DeviceId, StringComparison.OrdinalIgnoreCase)) exists = true;
                    if (!exists) ActiveDevices.Add(device);

                    Log($"Detected registered device: {device.DeviceName}. Preparing sync...");

                    // Show loading screen
                    _loadingWindow = new LoadingWindow();
                    _loadingWindow.UpdateStatus($"Connecting to {device.DeviceName}...", "Checking device data...");
                    _loadingWindow.Show();

                    // Run sync in background thread
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            _loadingWindow.UpdateStatus($"Syncing {device.DeviceName}...", "Scanning files...");
                            await _orchestrator.StartSyncAsync(device, GetNewToken());
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Sync error: {ex.Message}");
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                Log($"Sync error: {ex.Message}"));
                        }
                        finally
                        {
                            _loadingWindow?.Close();
                        }
                    });
                }
                else
                {
                    Log($"Detected unregistered device: {device.DeviceName}. Available for registration.");
                    bool exists = false;
                    foreach (var d in UnregisteredDevices)
                        if (string.Equals(d.DeviceId, device.DeviceId, StringComparison.OrdinalIgnoreCase)) exists = true;
                    if (!exists) UnregisteredDevices.Add(device);
                }
            });
        }

        private void OnMetricsUpdated(Models.SyncMetrics metrics)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (metrics.TotalFiles > 0)
                {
                    // Calculate progress based on both stages (Download + Upload)
                    // Total steps = TotalFiles * 2
                    // Completed steps = DownloadedFiles + UploadedFiles
                    double totalSteps = metrics.TotalFiles * 2;
                    double completedSteps = metrics.DownloadedFiles + metrics.UploadedFiles;
                    SyncProgressPercentage = (completedSteps / totalSteps) * 100;
                }
            });
        }

        private void OnSyncProgress(string message)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                StatusText = message;
                Log(message);
            });
        }

        private void OnSyncCompleted(string deviceId)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                IsSyncing = false;
                StatusText = "Sync completed. Waiting for devices...";
                Log($"Sync completed for device: {deviceId}");
            });
        }

        private void Log(string message)
        {
            string entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            Logs.Insert(0, entry);
            if (Logs.Count > 200) Logs.RemoveAt(200);
            _logger.LogInformation(message);
        }
    }
}
