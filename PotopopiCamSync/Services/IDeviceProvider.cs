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
        bool IsConnected { get; }

        Task ConnectAsync(CancellationToken cancellationToken = default);
        void Disconnect();

        Task<List<SyncFile>> GetFilesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads the file directly to the given destination stream.
        /// Avoids buffering the entire file in memory.
        /// </summary>
        Task DownloadToStreamAsync(SyncFile file, Stream destination, CancellationToken cancellationToken = default);

        Task DeleteFileAsync(SyncFile file, CancellationToken cancellationToken = default);
    }
}
