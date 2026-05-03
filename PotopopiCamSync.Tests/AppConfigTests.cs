using System;
using Newtonsoft.Json;
using PotopopiCamSync.Models;
using Xunit;

namespace PotopopiCamSync.Tests
{
    public class AppConfigTests
    {
        [Fact]
        public void AppConfig_Serialization_Includes_NewSettings()
        {
            var config = new AppConfig
            {
                AutoAlbumEnabled = true,
                StartMinimized = true,
                MinimizeToTray = false
            };
            
            var json = JsonConvert.SerializeObject(config);
            var deserialized = JsonConvert.DeserializeObject<AppConfig>(json);
            
            Assert.NotNull(deserialized);
            Assert.True(deserialized.AutoAlbumEnabled);
            Assert.True(deserialized.StartMinimized);
            Assert.False(deserialized.MinimizeToTray);
        }

        [Fact]
        public void DeviceSignature_Serialization_Includes_AutoAlbum()
        {
            var dev = new DeviceSignature
            {
                Id = "DEV1",
                ImmichAlbum = "Test Album",
                AutoAlbumEnabled = false
            };
            
            var json = JsonConvert.SerializeObject(dev);
            var deserialized = JsonConvert.DeserializeObject<DeviceSignature>(json);
            
            Assert.NotNull(deserialized);
            Assert.Equal("Test Album", deserialized.ImmichAlbum);
            Assert.False(deserialized.AutoAlbumEnabled);
        }
    }
}
