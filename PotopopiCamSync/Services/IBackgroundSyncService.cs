using System.Threading.Tasks;

namespace PotopopiCamSync.Services
{
    public interface IBackgroundSyncService
    {
        Task ScheduleBackgroundSyncAsync();
        Task CancelBackgroundSyncAsync();
    }
}
