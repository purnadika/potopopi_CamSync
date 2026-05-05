using System;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using PotopopiCamSync.Models;
using PotopopiCamSync.Utilities;

namespace PotopopiCamSync.Services
{
    public class GoogleDriveSyncService : ISyncDestination
    {
        private readonly ILogger<GoogleDriveSyncService> _logger;
        private readonly AppConfigModel _config;
        private readonly ITokenStorageService _tokenStorage;
        private DriveService? _driveService;

        public string Name => CloudConstants.GoogleDrive.ProviderName;
        public bool IsEnabled => _config.GoogleDriveAccount.IsEnabled && _config.GoogleDriveAccount.IsAuthenticated;

        public GoogleDriveSyncService(ILogger<GoogleDriveSyncService> logger, AppConfigModel config, ITokenStorageService tokenStorage)
        {
            _logger = logger;
            _config = config;
            _tokenStorage = tokenStorage;
        }

        public async Task InitializeAsync()
        {
            if (!_config.GoogleDriveAccount.IsEnabled) return;

            try
            {
                // We'll use a custom DataStore that uses our SecureTokenStorage
                var dataStore = new SecureDataStore(_tokenStorage, nameof(GoogleDriveSyncService));

                // If user provides their own Client ID, we use it. Otherwise we use a default.
                // For Desktop Apps, the Secret is not strictly 'secret' but required by the SDK.
                // We'll allow the user to provide both if they want, but default to empty secret.
                var clientId = string.IsNullOrWhiteSpace(_config.GoogleDriveAccount.ClientId) 
                    ? CloudConstants.GoogleDrive.DefaultClientId 
                    : _config.GoogleDriveAccount.ClientId;
                var clientSecret = string.IsNullOrWhiteSpace(_config.GoogleDriveAccount.ClientSecret) 
                    ? CloudConstants.GoogleDrive.DefaultClientSecret 
                    : _config.GoogleDriveAccount.ClientSecret;

                var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret },
                    CloudConstants.GoogleDrive.Scopes,
                    "user",
                    CancellationToken.None,
                    dataStore);

                _driveService = new DriveService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = AppConstants.General.ApplicationName,
                });
                
                _config.GoogleDriveAccount.IsAuthenticated = true;
                _config.GoogleDriveAccount.UserEmail = credential.UserId; // Might need to fetch profile for email
                _logger.LogInformation("{Prefix} Google Drive Sync Service initialized and authenticated.", CloudConstants.GoogleDrive.LogPrefix);
            }
            catch (Exception ex)
            {
                _config.GoogleDriveAccount.IsAuthenticated = false;
                _logger.LogError(ex, "{Prefix} Failed to initialize Google Drive: {Message}", CloudConstants.GoogleDrive.LogPrefix, ex.Message);
            }
        }

        public async Task<bool> UploadAsync(SyncFileModel file, string localFilePath, CancellationToken cancellationToken = default)
        {
            if (_driveService == null)
            {
                _logger.LogWarning("{Prefix} Google Drive service is not initialized or authenticated.", CloudConstants.GoogleDrive.LogPrefix);
                return false;
            }

            try
            {
                // Ensure target folder exists
                string folderName = _config.GoogleDriveAccount.TargetFolderName.TrimStart('/');
                if (string.IsNullOrEmpty(_config.GoogleDriveAccount.TargetFolderId))
                {
                    _config.GoogleDriveAccount.TargetFolderId = await GetOrCreateFolderAsync(folderName, cancellationToken);
                }
                
                _logger.LogInformation("{Prefix} Uploading {FileName}...", CloudConstants.GoogleDrive.LogPrefix, file.FileName);
                
                var fileMetadata = new Google.Apis.Drive.v3.Data.File()
                {
                    Name = file.FileName,
                    Parents = new List<string> { _config.GoogleDriveAccount.TargetFolderId }
                };

                using var stream = new FileStream(localFilePath, FileMode.Open);
                var request = _driveService.Files.Create(fileMetadata, stream, FileUtilities.GetMimeType(file.FileName));
                request.Fields = "id";
                
                var uploadProgress = await request.UploadAsync(cancellationToken);
                
                if (uploadProgress.Status == Google.Apis.Upload.UploadStatus.Completed)
                {
                    _logger.LogInformation("{Prefix} Uploaded {FileName} successfully.", CloudConstants.GoogleDrive.LogPrefix, file.FileName);
                    return true;
                }
                
                _logger.LogError("{Prefix} Upload failed for {FileName}: {Error}", CloudConstants.GoogleDrive.LogPrefix, file.FileName, uploadProgress.Exception?.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Failed to upload {FileName}: {Message}", CloudConstants.GoogleDrive.LogPrefix, file.FileName, ex.Message);
                return false;
            }
        }

        private async Task<string> GetOrCreateFolderAsync(string folderName, CancellationToken ct)
        {
            var listRequest = _driveService!.Files.List();
            listRequest.Q = $"name = '{folderName}' and mimeType = '{CloudConstants.GoogleDrive.FolderMimeType}' and trashed = false";
            var result = await listRequest.ExecuteAsync(ct);
            
            var folder = result.Files.FirstOrDefault();
            if (folder != null) return folder.Id;

            var newFolder = new Google.Apis.Drive.v3.Data.File()
            {
                Name = folderName,
                MimeType = CloudConstants.GoogleDrive.FolderMimeType
            };
            var createRequest = _driveService.Files.Create(newFolder);
            createRequest.Fields = "id";
            var createdFolder = await createRequest.ExecuteAsync(ct);
            return createdFolder.Id;
        }
    }
}
