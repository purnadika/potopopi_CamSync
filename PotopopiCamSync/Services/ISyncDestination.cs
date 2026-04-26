using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.Services
{
    public interface ISyncDestination
    {
        /// <summary>
        /// Upload a file from the given local path to this destination.
        /// Returns true on success.
        /// </summary>
        Task<bool> UploadAsync(SyncFile file, string localFilePath, CancellationToken cancellationToken = default);
    }
}
