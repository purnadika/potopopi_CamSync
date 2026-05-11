using System;
using System.IO;
using Newtonsoft.Json;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.Repositories
{
    public class JsonSettingsRepository : ISettingsRepository
    {
        private readonly string _configFilePath;
        private readonly string _stateFilePath;

        public AppConfigModel Config { get; private set; } = null!;
        public SyncStateModel State { get; private set; } = null!;

        public JsonSettingsRepository(string? customBasePath = null)
        {
            string appData = customBasePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PotopopiCamSync");
            if (!Directory.Exists(appData))
            {
                Directory.CreateDirectory(appData);
            }

            _configFilePath = Path.Combine(appData, "config.json");
            _stateFilePath = Path.Combine(appData, "syncstate.json");

            Load();
        }

        public void Load()
        {
            if (File.Exists(_configFilePath))
            {
                string json = File.ReadAllText(_configFilePath);
                Config = JsonConvert.DeserializeObject<AppConfigModel>(json) ?? new AppConfigModel();
            }
            else
            {
                Config = new AppConfigModel();
            }

            if (File.Exists(_stateFilePath))
            {
                string json = File.ReadAllText(_stateFilePath);
                State = JsonConvert.DeserializeObject<SyncStateModel>(json) ?? new SyncStateModel();
            }
            else
            {
                State = new SyncStateModel();
            }
        }

        public void SaveConfig()
        {
            string json = JsonConvert.SerializeObject(Config, Formatting.Indented);
            File.WriteAllText(_configFilePath, json);
        }

        public void SaveState()
        {
            string json = JsonConvert.SerializeObject(State, Formatting.Indented);
            File.WriteAllText(_stateFilePath, json);
        }
    }
}
