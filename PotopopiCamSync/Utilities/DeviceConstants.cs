using System;

namespace PotopopiCamSync.Utilities
{
    public static class DeviceConstants
    {
        public static readonly string[] MtpKeywords = new[] 
        { 
            "camera", "eos", "gopro", "hero", "portable device", "mtp", "digital still" 
        };

        public static readonly string[] ExcludeKeywords = new[] 
        { 
            "virtual camera", "integrated camera", "dfu" 
        };
    }
}
