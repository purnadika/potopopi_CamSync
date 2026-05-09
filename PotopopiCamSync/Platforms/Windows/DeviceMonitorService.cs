#if WINDOWS
using System;
using System.Management;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using Microsoft.Extensions.Logging;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.Services
{
    public class DeviceMonitorService : IDeviceMonitorService
    {
        private ManagementEventWatcher? _insertWatcher;
        private ManagementEventWatcher? _volumeWatcher;
        private readonly ILogger<DeviceMonitorService> _logger;
        

        public event Action<IDeviceProvider>? OnDeviceConnected;

        public DeviceMonitorService(ILogger<DeviceMonitorService> logger)
        {
            _logger = logger;
        }

        private bool IsMtpDevice(string? name, string? description)
        {
            if (string.IsNullOrEmpty(name)) return false;

            if (DeviceConstants.ExcludeKeywords.Any(exclude => 
                name.Contains(exclude, StringComparison.OrdinalIgnoreCase) || 
                (description != null && description.Contains(exclude, StringComparison.OrdinalIgnoreCase))))
            {
                return false;
            }

            return DeviceConstants.MtpKeywords.Any(keyword => 
                name.Contains(keyword, StringComparison.OrdinalIgnoreCase) || 
                (description != null && description.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            );
        }

        public void Start()
        {
            try
            {
                // Watch for PnP Devices (like MTP Cameras)
                WqlEventQuery insertQuery = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'");
                _insertWatcher = new ManagementEventWatcher(insertQuery);
                _insertWatcher.EventArrived += DeviceInsertedEvent;
                _insertWatcher.Start();

                // Watch for logical volumes (SD cards)
                WqlEventQuery volumeQuery = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_LogicalDisk'");
                _volumeWatcher = new ManagementEventWatcher(volumeQuery);
                _volumeWatcher.EventArrived += VolumeInsertedEvent;
                _volumeWatcher.Start();
                // Scan for already connected devices
                Task.Run(() => ScanExistingDevices());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start WMI watchers in DeviceMonitorService.");
            }
        }

        private void ScanExistingDevices()
        {
            try
            {
                // Scan Logical Disks (SD Cards)
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk WHERE DriveType = 2 OR DriveType = 3"))
                {
                    foreach (var device in searcher.Get())
                    {
                        var disk = WmiLogicalDiskModel.FromManagementObject(device);

                        if (!string.IsNullOrEmpty(disk.DriveLetter) && !string.IsNullOrEmpty(disk.VolumeSerialNumber))
                        {
                            string drivePath = disk.DriveLetter + "\\";
                            if (Directory.Exists(Path.Combine(drivePath, "DCIM")))
                            {
                                var provider = new SdCardDeviceProvider(disk.VolumeSerialNumber, disk.VolumeName ?? "SD Card", drivePath);
                                OnDeviceConnected?.Invoke(provider);
                            }
                        }
                    }
                }

                // Scan PnP Entities (MTP Cameras)
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity"))
                {
                    foreach (var device in searcher.Get())
                    {
                        var pnp = WmiPnpDeviceModel.FromManagementObject(device);

                        if (!string.IsNullOrEmpty(pnp.DeviceId) && IsMtpDevice(pnp.Name, pnp.Description))
                        {
                            var provider = new MtpDeviceProvider(pnp.DeviceId, pnp.Name!);
                            OnDeviceConnected?.Invoke(provider);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to scan existing devices on startup.");
            }
        }

        private void DeviceInsertedEvent(object sender, EventArrivedEventArgs e)
        {
            var instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            var pnp = WmiPnpDeviceModel.FromManagementObject(instance);

            if (!string.IsNullOrEmpty(pnp.DeviceId) && IsMtpDevice(pnp.Name, pnp.Description))
            {
                var provider = new MtpDeviceProvider(pnp.DeviceId, pnp.Name!);
                OnDeviceConnected?.Invoke(provider);
            }
        }

        private void VolumeInsertedEvent(object sender, EventArrivedEventArgs e)
        {
            var instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            var disk = WmiLogicalDiskModel.FromManagementObject(instance);

            if (!string.IsNullOrEmpty(disk.DriveLetter) && !string.IsNullOrEmpty(disk.VolumeSerialNumber))
            {
                string drivePath = disk.DriveLetter + "\\";
                // Only consider it if it might be an SD card with a DCIM folder, or we just notify
                // and let the Orchestrator decide based on registered devices.
                if (Directory.Exists(Path.Combine(drivePath, "DCIM")))
                {
                    var provider = new SdCardDeviceProvider(disk.VolumeSerialNumber, disk.VolumeName ?? "SD Card", drivePath);
                    OnDeviceConnected?.Invoke(provider);
                }
            }
        }

        public void Stop()
        {
            _insertWatcher?.Stop();
            _volumeWatcher?.Stop();
        }

        public void Dispose()
        {
            _insertWatcher?.Dispose();
            _volumeWatcher?.Dispose();
        }
    }
}
#endif
