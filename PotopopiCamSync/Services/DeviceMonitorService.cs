using System;
using System.Management;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using Microsoft.Extensions.Logging;

namespace PotopopiCamSync.Services
{
    public class DeviceMonitorService : IDisposable
    {
        private ManagementEventWatcher? _insertWatcher;
        private ManagementEventWatcher? _volumeWatcher;
        private readonly ILogger<DeviceMonitorService> _logger;
        
        public event Action<IDeviceProvider> OnDeviceConnected;

        public DeviceMonitorService(ILogger<DeviceMonitorService> logger)
        {
            _logger = logger;
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
            }
            catch (Exception)
            {
                // Handle or log WMI exceptions (might require admin rights depending on OS settings, but usually ok)
            }
        }

        private void DeviceInsertedEvent(object sender, EventArrivedEventArgs e)
        {
            var instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            string? deviceId = instance["PNPDeviceID"]?.ToString();
            string? name = instance["Name"]?.ToString();
            string? description = instance["Description"]?.ToString();

            // MTP devices often have "Portable Device" or "MTP" or "Digital Still Camera" in description or service
            if (!string.IsNullOrEmpty(deviceId) && !string.IsNullOrEmpty(name))
            {
                if (name.Contains("camera", StringComparison.OrdinalIgnoreCase) || 
                    (description is not null && description.Contains("camera", StringComparison.OrdinalIgnoreCase)) || 
                    name.Contains("eos", StringComparison.OrdinalIgnoreCase) || 
                    name.Contains("portable device", StringComparison.OrdinalIgnoreCase) || 
                    (description is not null && description.Contains("portable device", StringComparison.OrdinalIgnoreCase)))
                {
                    var provider = new MtpDeviceProvider(deviceId, name);
                    OnDeviceConnected?.Invoke(provider);
                }
            }
        }

        private void VolumeInsertedEvent(object sender, EventArrivedEventArgs e)
        {
            var instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            string? driveLetter = instance["DeviceID"]?.ToString(); // e.g. E:
            string? volumeSerialNumber = instance["VolumeSerialNumber"]?.ToString();
            string? volumeName = instance["VolumeName"]?.ToString();

            if (!string.IsNullOrEmpty(driveLetter) && !string.IsNullOrEmpty(volumeSerialNumber))
            {
                string drivePath = driveLetter + "\\";
                // Only consider it if it might be an SD card with a DCIM folder, or we just notify
                // and let the Orchestrator decide based on registered devices.
                if (Directory.Exists(Path.Combine(drivePath, "DCIM")))
                {
                    var provider = new SdCardDeviceProvider(volumeSerialNumber, volumeName ?? "SD Card", drivePath);
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
