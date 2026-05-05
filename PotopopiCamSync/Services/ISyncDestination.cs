using System.Threading;
using System.Threading.Tasks;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.Services
{
    public interface ISyncDestination
    {
        string Name { get; }
        bool IsEnabled { get; }
        Task<bool> UploadAsync(SyncFileModel file, string localFilePath, CancellationToken cancellationToken = default);
    }
}
