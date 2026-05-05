using System;

namespace PotopopiCamSync.Utilities
{
    public static class MessageConstants
    {
        public static class General
        {
            public const string AlreadyRunning = "Potopopi CamSync is already running!";
            public const string RunningBackground = "Potopopi CamSync is active in the system tray.";
            public const string DiskSpaceInsufficient = "⚠ Insufficient disk space.";
            public const string LocalBackupFolderNotSet = "Local Backup Folder is not configured. Please set it in Settings.";
            public const string FailedToConnect = "Failed to connect to device.";
            public const string ScanningFiles = "Scanning for files...";
            public const string SyncCancelled = "Sync cancelled.";
            public const string SyncComplete = "✓ Process complete.";
            public const string ImmichInaccessible = "✗ Immich Server Inaccessible or Down.";
        }

        public static class Sync
        {
            public const string ConnectingToDevice = "Connecting to {0}...";
            public const string SmartScanFound = "Smart Scan: {0} new file(s) found.";
            public const string ScanCompleteWithCount = "Scan complete. Found {0} file(s).";
            public const string ForceSyncRedownload = "  [Force Sync] Re-downloading missing/incomplete file: {0}";
            public const string ProgressDownload = "[{0}/{1}] Downloading to Local: {2}";
            public const string AIAnalysisStart = "  → [AI Analysis] {0}";
            public const string AIAnalysisBlurry = "  ⚠ [AI] {0} looks blurry ({1:F0})";
            public const string AISkippingImmich = "  ✗ Skipping Immich upload (AI Rejection). Local copy saved.";
            public const string UploadingToDestination = "  → [{0}] Uploading {1}";
            public const string UploadFailed = "  ✗ [{0}] Failed: {1}";
            public const string LocalToImmichFound = "Found {0} file(s). Uploading to {1}...";
            public const string LocalToImmichProgress = "[{0}] → {1} Upload: {2}";
            public const string LocalToImmichSkippingAI = "  ✗ Skipping {0} (AI Rejection). Not whitelisted.";
            public const string LocalToImmichFailed = "  ✗ Failed: {0}";
            public const string LocalToImmichComplete = "✓ Local→{0} upload complete.";
            public const string ImmichNotSet = "Immich is not configured.";
        }
    }
}
