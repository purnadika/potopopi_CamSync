using System;
using System.Collections.Generic;

namespace PotopopiCamSync.Models
{
    public class AppConfig
    {
        public bool FirstRunCompleted { get; set; } = false;
        public string LocalBackupFolder { get; set; } = string.Empty;
        public bool DeleteAfterSync { get; set; } = false;
        public int KeepFilesDays { get; set; } = 0; // 0 means keep forever (or delete immediately if DeleteAfterSync is true and KeepFilesDays is 0)

        // Immich config
        public string ImmichUrl { get; set; } = string.Empty;
        public string ImmichApiKey { get; set; } = string.Empty;
        public bool EnableImmichSync { get; set; } = false;

        // Performance throttling (bytes per second, 0 = unlimited)
        public long DownloadSpeedLimitBps { get; set; } = 0;
        public long UploadSpeedLimitBps { get; set; } = 0;

        // Exclusion filters (glob patterns, comma-separated)
        public string ExclusionPatterns { get; set; } = "*.tmp,*.bak,.DS_Store,Thumbs.db";

        public List<DeviceSignature> RegisteredDevices { get; set; } = new List<DeviceSignature>();
    }

    public class DeviceSignature
    {
        public string Id { get; set; } = string.Empty; // e.g. Volume Serial or MTP PnP Device ID
        public string Type { get; set; } = "Mtp"; // Mtp or SdCard
        public string Name { get; set; } = string.Empty;
        public string ImmichAlbum { get; set; } = string.Empty; // Album name in Immich (optional)
    }
}
