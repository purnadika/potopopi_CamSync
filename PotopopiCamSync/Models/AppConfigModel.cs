using System;
using System.Collections.Generic;

namespace PotopopiCamSync.Models
{
    public class AppConfigModel
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
        public List<ImmichAccountModel> ImmichAccounts { get; set; } = new();

        // Multi-Cloud support
        public GoogleDriveAccountModel GoogleDriveAccount { get; set; } = new();
        public OneDriveAccountModel OneDriveAccount { get; set; } = new();

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

        public List<DeviceSignatureModel> RegisteredDevices { get; set; } = new List<DeviceSignatureModel>();
    }
}
