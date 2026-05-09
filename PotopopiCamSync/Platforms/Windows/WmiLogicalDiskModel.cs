#if WINDOWS
using System.Management;

namespace PotopopiCamSync.Models
{
    public class WmiLogicalDiskModel
    {
        public string? DriveLetter { get; set; }
        public string? VolumeSerialNumber { get; set; }
        public string? VolumeName { get; set; }

        public static WmiLogicalDiskModel FromManagementObject(ManagementBaseObject instance)
        {
            return new WmiLogicalDiskModel
            {
                DriveLetter = instance["DeviceID"]?.ToString(),
                VolumeSerialNumber = instance["VolumeSerialNumber"]?.ToString(),
                VolumeName = instance["VolumeName"]?.ToString()
            };
        }
    }
}
#endif
