using System;
using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PotopopiCamSync.Services;

namespace PotopopiCamSync.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly SyncOrchestrator _orchestrator;
        private readonly DeviceMonitorService _deviceMonitor;
        private readonly SettingsService _settingsService;
        private readonly ILogger<MainViewModel> _logger;

        private CancellationTokenSource? _syncCts;

        [ObservableProperty]
        private string _statusText = "Waiting for devices...";

        [ObservableProperty]
        private bool _isSyncing;

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
            System.Windows.Application.Current.Dispatcher.Invoke(async () =>
            {
                bool isRegistered = _settingsService.Config.RegisteredDevices.Exists(d =>
                    string.Equals(d.Id, device.DeviceId, StringComparison.OrdinalIgnoreCase));

                if (isRegistered)
                {
                    bool exists = false;
                    foreach (var d in ActiveDevices)
                        if (string.Equals(d.DeviceId, device.DeviceId, StringComparison.OrdinalIgnoreCase)) exists = true;
                    if (!exists) ActiveDevices.Add(device);

                    Log($"Detected registered device: {device.DeviceName}. Starting sync...");
                    await _orchestrator.StartSyncAsync(device, GetNewToken());
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
