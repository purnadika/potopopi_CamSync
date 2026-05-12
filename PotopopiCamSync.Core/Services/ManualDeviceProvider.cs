using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.Services
{
    public class ManualDeviceProvider : IDeviceProvider
    {
        private readonly List<SyncFileModel> _files;
        
        public string DeviceId { get; }
        public string DeviceName { get; }
        public bool IsConnected { get; private set; }

        public ManualDeviceProvider(string deviceId, string deviceName, List<SyncFileModel> files)
        {
            DeviceId = deviceId;
            DeviceName = deviceName;
            _files = files;
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = true;
            return Task.CompletedTask;
        }

        public void Disconnect()
        {
            IsConnected = false;
        }

        public Task<List<SyncFileModel>> GetFilesAsync(CancellationToken cancellationToken = default, Action<string>? progressCallback = null)
        {
            return Task.FromResult(_files);
        }

        public async Task DownloadToStreamAsync(SyncFileModel file, Stream destination, CancellationToken cancellationToken = default)
        {
            // On Android, if we have the absolute path, we attempt to open it directly.
            // If raw path access fails, this will throw, which is expected for diagnostics.
            using var fs = new FileStream(file.OriginalPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            await fs.CopyToAsync(destination, cancellationToken);
        }

        public Task DeleteFileAsync(SyncFileModel file, CancellationToken cancellationToken = default)
        {
            if (File.Exists(file.OriginalPath))
            {
                File.Delete(file.OriginalPath);
            }
            return Task.CompletedTask;
        }
    }
}
