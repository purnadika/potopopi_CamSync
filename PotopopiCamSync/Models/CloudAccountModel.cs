using System;
using PotopopiCamSync.Utilities;

namespace PotopopiCamSync.Models
{
    public enum CloudProviderType
    {
        Immich,
        GoogleDrive,
        OneDrive
    }

    public abstract class CloudAccountBase
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = false;
        public bool IsAuthenticated { get; set; } = false;
        public string UserEmail { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public DateTime LastSyncTime { get; set; } = DateTime.MinValue;
        public abstract CloudProviderType ProviderType { get; }
    }

    public class GoogleDriveAccountModel : CloudAccountBase
    {
        public override CloudProviderType ProviderType => CloudProviderType.GoogleDrive;
        public string TargetFolderId { get; set; } = string.Empty;
        public string TargetFolderName { get; set; } = "/" + CloudConstants.General.DefaultTargetFolderName;
    }

    public class OneDriveAccountModel : CloudAccountBase
    {
        public override CloudProviderType ProviderType => CloudProviderType.OneDrive;
        public string TargetFolderId { get; set; } = string.Empty;
        public string TargetFolderName { get; set; } = "/" + CloudConstants.General.DefaultTargetFolderName;
    }
}
