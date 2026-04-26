using System.IO;
using System.Threading.Tasks;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.Services
{
    public interface ISyncDestination
    {
        Task<bool> UploadAsync(SyncFile file, Stream fileStream);
    }
}
