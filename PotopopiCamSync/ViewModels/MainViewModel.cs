using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PotopopiCamSync.Services;

namespace PotopopiCamSync.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly SyncOrchestrator _orchestrator;
        private readonly DeviceMonitorService _deviceMonitor;
        private readonly SettingsService _settingsService;

        [ObservableProperty]
        private string _statusText = "Waiting for devices...";

        public ObservableCollection<string> Logs { get; } = new ObservableCollection<string>();
        public ObservableCollection<IDeviceProvider> UnregisteredDevices { get; } = new ObservableCollection<IDeviceProvider>();
        public ObservableCollection<IDeviceProvider> ActiveDevices { get; } = new ObservableCollection<IDeviceProvider>();

        [RelayCommand]
        private void ManualSync(IDeviceProvider device)
        {
            if (device != null)
            {
                Log($"Manual sync triggered for {device.DeviceName}...");
                _ = _orchestrator.StartSyncAsync(device);
            }
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
            Log($"Registered device: {device.DeviceName}. Starting sync...");

            _ = _orchestrator.StartSyncAsync(device);
        }

        public MainViewModel(SyncOrchestrator orchestrator, DeviceMonitorService deviceMonitor, SettingsService settingsService)
        {
            _orchestrator = orchestrator;
            _deviceMonitor = deviceMonitor;
            _settingsService = settingsService;

            _orchestrator.OnSyncProgress += OnSyncProgress;
            _orchestrator.OnSyncCompleted += OnSyncCompleted;
            _deviceMonitor.OnDeviceConnected += OnDeviceConnected;
            
            _deviceMonitor.Start();
        }

        private void OnDeviceConnected(IDeviceProvider device)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(async () =>
            {
                bool isRegistered = _settingsService.Config.RegisteredDevices.Exists(d => d.Id == device.DeviceId);
                if (isRegistered)
                {
                    bool exists = false;
                    foreach(var d in ActiveDevices) if (d.DeviceId == device.DeviceId) exists = true;
                    if (!exists) ActiveDevices.Add(device);

                    Log($"Detected registered device: {device.DeviceName}. Starting sync...");
                    await _orchestrator.StartSyncAsync(device);
                }
                else
                {
                    Log($"Detected unregistered device: {device.DeviceName}. Available for registration.");
                    
                    bool exists = false;
                    foreach(var d in UnregisteredDevices)
                    {
                        if(d.DeviceId == device.DeviceId) exists = true;
                    }
                    if (!exists)
                    {
                        UnregisteredDevices.Add(device);
                    }
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
                StatusText = "Sync completed. Waiting for devices...";
                Log($"Sync completed for device ID: {deviceId}");
            });
        }

        private void Log(string message)
        {
            string entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            Logs.Insert(0, entry);
            if (Logs.Count > 100) Logs.RemoveAt(100);

            // Also write to file log
            FileLogger.Log(message);
        }
    }
}
