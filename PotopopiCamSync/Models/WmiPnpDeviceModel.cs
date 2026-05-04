using System.Management;

namespace PotopopiCamSync.Models
{
    public class WmiPnpDeviceModel
    {
        public string? DeviceId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }

        public static WmiPnpDeviceModel FromManagementObject(ManagementBaseObject instance)
        {
            return new WmiPnpDeviceModel
            {
                DeviceId = instance["PNPDeviceID"]?.ToString(),
                Name = instance["Name"]?.ToString(),
                Description = instance["Description"]?.ToString()
            };
        }
    }
}
