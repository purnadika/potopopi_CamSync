using System;
using System.Collections.Generic;

namespace PotopopiCamSync.Models
{
    public class AppConfig
    {
        public bool FirstRunCompleted { get; set; } = false;
        public string LocalBackupFolder { get; set; } = string.Empty;
        public bool DeleteAfterSync { get; set; } = false;
        public int KeepFilesDays { get; set; } = 0; // 0 means keep forever

        // Immich config (Global/Legacy)
        public string ImmichUrl { get; set; } = string.Empty;
        public string ImmichApiKey { get; set; } = string.Empty;
        public bool EnableImmichSync { get; set; } = false;

        // New Multi-Account support
        public List<ImmichAccount> ImmichAccounts { get; set; } = new();

        // AI Config
        public AIAnalysisMode AIAnalysisMode { get; set; } = AIAnalysisMode.Standard;

        // Performance throttling (bytes per second, 0 = unlimited)
        public long DownloadSpeedLimitBps { get; set; } = 0;
        public long UploadSpeedLimitBps { get; set; } = 0;

        // Exclusion filters (glob patterns, comma-separated)
        public string ExclusionPatterns { get; set; } = "*.tmp,*.bak,.DS_Store,Thumbs.db";
        public string ImmichExclusionPatterns { get; set; } = "*.cr2,*.cr3,*.arw,*.dng,*.nef,*.orf";
        
        public bool AutoAlbumEnabled { get; set; } = true;
        public bool StartMinimized { get; set; } = false;
        public bool MinimizeToTray { get; set; } = true;

        public List<DeviceSignature> RegisteredDevices { get; set; } = new List<DeviceSignature>();
    }

    public enum AIAnalysisMode
    {
        None,
        Standard,   // Blur + pHash (OpenCV)
        Balanced,   // CPU AI (ONNX)
        Extreme     // GPU AI (ONNX + CUDA/DirectML)
    }

    public class DeviceSignature
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
