using System;
using System.IO;
using Newtonsoft.Json;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.Services
{
    public class SettingsService
    {
        private readonly string _configFilePath;
        private readonly string _stateFilePath;

        public AppConfig Config { get; private set; } = null!;
        public SyncState State { get; private set; } = null!;

        public SettingsService(string? customBasePath = null)
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
                Config = JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
            }
            else
            {
                Config = new AppConfig();
            }

            if (File.Exists(_stateFilePath))
            {
                string json = File.ReadAllText(_stateFilePath);
                State = JsonConvert.DeserializeObject<SyncState>(json) ?? new SyncState();
            }
            else
            {
                State = new SyncState();
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
