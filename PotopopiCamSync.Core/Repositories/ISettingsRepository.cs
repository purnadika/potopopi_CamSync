using PotopopiCamSync.Models;

namespace PotopopiCamSync.Repositories
{
    public interface ISettingsRepository
    {
        AppConfigModel Config { get; }
        SyncStateModel State { get; }
        void Load();
        void SaveConfig();
        void SaveState();
    }
}
