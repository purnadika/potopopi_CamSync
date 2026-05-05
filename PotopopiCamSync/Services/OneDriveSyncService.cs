using System;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Graph;
using PotopopiCamSync.Models;
using PotopopiCamSync.Utilities;

namespace PotopopiCamSync.Services
{
    public class OneDriveSyncService : ISyncDestination
    {
        private readonly ILogger<OneDriveSyncService> _logger;
        private readonly AppConfigModel _config;
        private readonly ITokenStorageService _tokenStorage;
        private GraphServiceClient? _graphClient;

        public string Name => CloudConstants.OneDrive.ProviderName;
        public bool IsEnabled => _config.OneDriveAccount.IsEnabled && _config.OneDriveAccount.IsAuthenticated;

        public OneDriveSyncService(ILogger<OneDriveSyncService> logger, AppConfigModel config, ITokenStorageService tokenStorage)
        {
            _logger = logger;
            _config = config;
            _tokenStorage = tokenStorage;
        }

        public async Task InitializeAsync()
        {
            if (!_config.OneDriveAccount.IsEnabled) return;

            try
            {
                // Placeholder for InteractiveBrowserCredential with PKCE
                // For a desktop app, we use ClientId and "common" or "consumers" tenant.
                string clientId = _config.OneDriveAccount.ClientId;
                if (string.IsNullOrWhiteSpace(clientId)) clientId = CloudConstants.OneDrive.DefaultClientId;

                var options = new InteractiveBrowserCredentialOptions
                {
                    TenantId = CloudConstants.OneDrive.Tenant,
                    ClientId = clientId,
                    // The redirect URI for desktop apps is usually http://localhost
                    RedirectUri = new Uri(CloudConstants.General.DefaultRedirectUri)
                };

                // Note: To make this persistent, we should implement a TokenCache persistence layer.
                // Azure.Identity handles some of this, but we can also manually handle it if needed.
                var interactiveCredential = new InteractiveBrowserCredential(options);

                _graphClient = new GraphServiceClient(interactiveCredential, CloudConstants.OneDrive.Scopes);
                
                // Trigger a silent check
                var user = await _graphClient.Me.GetAsync();
                
                _config.OneDriveAccount.IsAuthenticated = true;
                _config.OneDriveAccount.UserEmail = user?.Mail ?? user?.UserPrincipalName ?? string.Empty;
                _logger.LogInformation("{Prefix} OneDrive Sync Service initialized. Authenticated as {User}", CloudConstants.OneDrive.LogPrefix, _config.OneDriveAccount.UserEmail);
            }
            catch (Exception ex)
            {
                _config.OneDriveAccount.IsAuthenticated = false;
                _logger.LogError(ex, "{Prefix} Failed to initialize OneDrive: {Message}", CloudConstants.OneDrive.LogPrefix, ex.Message);
            }
        }

        public async Task<bool> UploadAsync(SyncFileModel file, string localFilePath, CancellationToken cancellationToken = default)
        {
            if (_graphClient == null)
            {
                _logger.LogWarning("{Prefix} OneDrive service is not initialized or authenticated.", CloudConstants.OneDrive.LogPrefix);
                return false;
            }

            try
            {
                string folderName = _config.OneDriveAccount.TargetFolderName.TrimStart('/');
                _logger.LogInformation("{Prefix} Uploading {FileName}...", CloudConstants.OneDrive.LogPrefix, file.FileName);

                // Simple upload for small files (up to 4MB) or session-based for large
                using var stream = new FileStream(localFilePath, FileMode.Open);
                
                // In Microsoft Graph v5, we can use the drives["me"] alias
                var uploadRequest = _graphClient.Drives["me"].Items[$"root:/{folderName}/{file.FileName}:"]
                    .Content;

                await uploadRequest.PutAsync(stream, null, cancellationToken);
                
                _logger.LogInformation("{Prefix} Uploaded {FileName} successfully.", CloudConstants.OneDrive.LogPrefix, file.FileName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Failed to upload {FileName}: {Message}", CloudConstants.OneDrive.LogPrefix, file.FileName, ex.Message);
                return false;
            }
        }
    }
}
