using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.Services
{
    public class LocalFolderSync : ISyncDestination
    {
        private readonly string _targetFolder;
        private readonly ILogger<LocalFolderSync> _logger;

        public LocalFolderSync(string targetFolder, ILogger<LocalFolderSync> logger)
        {
            _targetFolder = targetFolder;
            _logger = logger;
        }

        /// <summary>
        /// Copies a file from localFilePath into the local backup folder structure (yyyy-MM-dd\filename).
        /// Returns the destination file path on success so the orchestrator can use it for further uploads.
        /// </summary>
        public async Task<bool> UploadAsync(SyncFile file, string localFilePath, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(_targetFolder)) return false;

                string dateFolder = file.CreationTime.ToString("yyyy-MM-dd");
                string finalDirPath = Path.Combine(_targetFolder, dateFolder);
                Directory.CreateDirectory(finalDirPath);

                string targetPath = Path.Combine(finalDirPath, file.FileName);

                // Skip if already there with same size
                if (File.Exists(targetPath) && new FileInfo(targetPath).Length == file.Size)
                    return true;

                using var source = File.OpenRead(localFilePath);
                using var dest = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
                await source.CopyToAsync(dest, cancellationToken);

                _logger.LogInformation("Local backup saved: {Path}", targetPath);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save local backup for {File}", file.FileName);
                return false;
            }
        }

        /// <summary>
        /// Returns the expected local backup path for a given file, regardless of whether it exists yet.
        /// </summary>
        public string GetLocalBackupPath(SyncFile file)
        {
            string dateFolder = file.CreationTime.ToString("yyyy-MM-dd");
            return Path.Combine(_targetFolder, dateFolder, file.FileName);
        }
    }
}
