using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.Services
{
    public interface IDeviceProvider
    {
        string DeviceId { get; }
        string DeviceName { get; }
        string DisplayName => $"{DeviceName} ({DeviceId})";
        bool IsConnected { get; }

        Task ConnectAsync(CancellationToken cancellationToken = default);
        void Disconnect();
        Task<List<SyncFileModel>> GetFilesAsync(CancellationToken cancellationToken = default, System.Action<string>? progressCallback = null);

        /// <summary>
        /// Downloads the file directly to the given destination stream.
        /// Avoids buffering the entire file in memory.
        /// </summary>
        Task DownloadToStreamAsync(SyncFileModel file, Stream destination, CancellationToken cancellationToken = default);

        Task DeleteFileAsync(SyncFileModel file, CancellationToken cancellationToken = default);
    }
}
