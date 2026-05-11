using System;

namespace PotopopiCamSync.Models
{
    public class DeviceSignatureModel
    {
        public string Id { get; set; } = string.Empty; // e.g. Volume Serial or MTP PnP Device ID
        public string Type { get; set; } = "Mtp"; // Mtp or SdCard
        public string Name { get; set; } = string.Empty;
        public string ImmichAlbum { get; set; } = string.Empty; // Album name in Immich (optional)
        public bool AutoAlbumEnabled { get; set; } = true;

        // Profile Overrides
        public string? LocalFolderOverride { get; set; }

        public string? ImmichAccountId { get; set; }
        public DateTime? LastSyncDate { get; set; }
        public bool UseSmartScan { get; set; } = true;
    }
}
