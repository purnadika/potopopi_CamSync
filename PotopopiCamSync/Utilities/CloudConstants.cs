using System;

namespace PotopopiCamSync.Utilities
{
    public static class CloudConstants
    {
        public static class GoogleDrive
        {
            public const string ProviderName = "Google Drive";
            public const string LogPrefix = "[Google Drive]";
            public const string DefaultClientId = "YOUR_DEFAULT_GOOGLE_CLIENT_ID";
            public const string DefaultClientSecret = "YOUR_DEFAULT_GOOGLE_CLIENT_SECRET";
            public const string FolderMimeType = "application/vnd.google-apps.folder";
            public static readonly string[] Scopes = { "https://www.googleapis.com/auth/drive.file" };
        }

        public static class OneDrive
        {
            public const string ProviderName = "OneDrive";
            public const string LogPrefix = "[OneDrive]";
            public const string DefaultClientId = "YOUR_DEFAULT_ONEDRIVE_CLIENT_ID";
            public static readonly string[] Scopes = { "Files.ReadWrite.All", "User.Read" };
            public const string Tenant = "common";
        }

        public static class Immich
        {
            public const string ProviderName = "Immich";
            public const string LogPrefix = "[Immich]";
        }

        public static class General
        {
            public const string DefaultTargetFolderName = AppConstants.General.InternalName;
            // Standard Loopback flow for Desktop Apps
            public const string DefaultRedirectUri = "http://localhost";
        }
    }
}
