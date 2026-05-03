using PotopopiCamSync.Models;

namespace PotopopiCamSync.Repositories
{
    public interface ISettingsRepository
    {
        AppConfig Config { get; }
        SyncState State { get; }
        void Load();
        void SaveConfig();
        void SaveState();
    }
}
