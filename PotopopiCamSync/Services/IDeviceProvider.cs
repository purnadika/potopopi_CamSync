using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.Services
{
    public interface IDeviceProvider
    {
        string DeviceId { get; }
        string DeviceName { get; }
        bool IsConnected { get; }
        
        Task ConnectAsync();
        void Disconnect();
        
        Task<List<SyncFile>> GetFilesAsync();
        Task<Stream> GetFileStreamAsync(SyncFile file);
        Task DeleteFileAsync(SyncFile file);
    }
}
