using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using PotopopiCamSync.Models;
using PotopopiCamSync.Services;

namespace PotopopiCamSync.Tests
{
    /// <summary>
    /// Tests for SettingsWindow device management functionality
    /// Tests the unregister and refresh device list operations
    /// </summary>
    public class SettingsWindowDeviceManagementTests : IDisposable
    {
        private AppConfig _config;
        private List<DeviceSignature> _devices;

        public SettingsWindowDeviceManagementTests()
        {
            _config = new AppConfig();
            _devices = _config.RegisteredDevices;
        }

        public void Dispose()
        {
            _devices.Clear();
        }

        /// <summary>
        /// Test: RegisteredDevices list is initialized as empty
        /// </summary>
        [Fact]
        public void RegisteredDevices_InitializedAsEmpty()
        {
            Assert.Empty(_devices);
        }

        /// <summary>
        /// Test: Can add a device to registered devices
        /// </summary>
        [Fact]
        public void AddDevice_Success()
        {
            var device = new DeviceSignature
            {
                Id = "DEVICE_001",
                Type = "Mtp",
                Name = "Canon Camera"
            };

            _devices.Add(device);

            Assert.Single(_devices);
            Assert.Equal("DEVICE_001", _devices[0].Id);
            Assert.Equal("Canon Camera", _devices[0].Name);
        }

        /// <summary>
        /// Test: Can add multiple devices
        /// </summary>
        [Fact]
        public void AddMultipleDevices_Success()
        {
            _devices.Add(new DeviceSignature { Id = "DEVICE_001", Type = "Mtp", Name = "Camera 1" });
            _devices.Add(new DeviceSignature { Id = "DEVICE_002", Type = "SdCard", Name = "SD Card Reader" });
            _devices.Add(new DeviceSignature { Id = "DEVICE_003", Type = "Mtp", Name = "Camera 2" });

            Assert.Equal(3, _devices.Count);
        }

        /// <summary>
        /// Test: Unregister removes device from list by ID
        /// </summary>
        [Fact]
        public void UnregisterDevice_RemovesByID()
        {
            // Arrange
            _devices.Add(new DeviceSignature { Id = "DEVICE_001", Type = "Mtp", Name = "Camera 1" });
            _devices.Add(new DeviceSignature { Id = "DEVICE_002", Type = "SdCard", Name = "SD Card Reader" });
            _devices.Add(new DeviceSignature { Id = "DEVICE_003", Type = "Mtp", Name = "Camera 2" });

            // Act
            var removed = _devices.RemoveAll(d => d.Id == "DEVICE_002");

            // Assert
            Assert.Equal(1, removed);
            Assert.Equal(2, _devices.Count);
            Assert.Null(_devices.FirstOrDefault(d => d.Id == "DEVICE_002"));
            Assert.Single(_devices, d => d.Name == "Camera 1");
        }

        /// <summary>
        /// Test: Unregister non-existent device returns no removal
        /// </summary>
        [Fact]
        public void UnregisterDevice_NonExistent_NoRemoval()
        {
            // Arrange
            _devices.Add(new DeviceSignature { Id = "DEVICE_001", Type = "Mtp", Name = "Camera 1" });

            // Act
            var removed = _devices.RemoveAll(d => d.Id == "NONEXISTENT");

            // Assert
            Assert.Equal(0, removed);
            Assert.Single(_devices);
        }

        /// <summary>
        /// Test: Unregister all devices of specific type
        /// </summary>
        [Fact]
        public void UnregisterDevicesByType_RemovesOnlySpecificType()
        {
            // Arrange
            _devices.Add(new DeviceSignature { Id = "MTP_001", Type = "Mtp", Name = "Camera 1" });
            _devices.Add(new DeviceSignature { Id = "SD_001", Type = "SdCard", Name = "SD Card 1" });
            _devices.Add(new DeviceSignature { Id = "MTP_002", Type = "Mtp", Name = "Camera 2" });
            _devices.Add(new DeviceSignature { Id = "SD_002", Type = "SdCard", Name = "SD Card 2" });

            // Act
            var removed = _devices.RemoveAll(d => d.Type == "SdCard");

            // Assert
            Assert.Equal(2, removed);
            Assert.Equal(2, _devices.Count);
            Assert.All(_devices, d => Assert.Equal("Mtp", d.Type));
        }

        /// <summary>
        /// Test: Device name can be updated (refresh list)
        /// </summary>
        [Fact]
        public void UpdateDeviceName_Success()
        {
            // Arrange
            var device = new DeviceSignature { Id = "DEVICE_001", Type = "Mtp", Name = "Camera" };
            _devices.Add(device);

            // Act
            var found = _devices.FirstOrDefault(d => d.Id == "DEVICE_001");
            if (found != null)
                found.Name = "New Camera Name";

            // Assert
            Assert.Equal("New Camera Name", _devices[0].Name);
        }

        /// <summary>
        /// Test: Device with album assignment is preserved
        /// </summary>
        [Fact]
        public void DeviceWithAlbum_PreservedAfterRefresh()
        {
            // Arrange
            var device = new DeviceSignature
            {
                Id = "DEVICE_001",
                Type = "Mtp",
                Name = "Camera",
                ImmichAlbum = "Vacation 2025"
            };
            _devices.Add(device);

            // Act
            // Simulate refresh: clear and repopulate (actual refresh would reload from storage)
            var newConfig = new AppConfig();
            newConfig.RegisteredDevices.Add(device);

            // Assert
            Assert.Single(newConfig.RegisteredDevices);
            Assert.Equal("Vacation 2025", newConfig.RegisteredDevices[0].ImmichAlbum);
        }

        /// <summary>
        /// Test: Empty device list shows no devices
        /// </summary>
        [Fact]
        public void DeviceList_Empty_IsEmpty()
        {
            Assert.Empty(_devices);
        }

        /// <summary>
        /// Test: Device list can be cleared
        /// </summary>
        [Fact]
        public void ClearAllDevices_Success()
        {
            // Arrange
            _devices.Add(new DeviceSignature { Id = "DEVICE_001", Type = "Mtp", Name = "Camera 1" });
            _devices.Add(new DeviceSignature { Id = "DEVICE_002", Type = "SdCard", Name = "SD Card" });

            // Act
            _devices.Clear();

            // Assert
            Assert.Empty(_devices);
        }

        /// <summary>
        /// Test: Find device by ID for display/edit
        /// </summary>
        [Fact]
        public void FindDeviceById_Success()
        {
            // Arrange
            _devices.Add(new DeviceSignature { Id = "DEVICE_001", Type = "Mtp", Name = "Camera 1" });
            _devices.Add(new DeviceSignature { Id = "DEVICE_002", Type = "SdCard", Name = "SD Card" });
            _devices.Add(new DeviceSignature { Id = "DEVICE_003", Type = "Mtp", Name = "Camera 2" });

            // Act
            var device = _devices.FirstOrDefault(d => d.Id == "DEVICE_002");

            // Assert
            Assert.NotNull(device);
            Assert.Equal("SD Card", device.Name);
            Assert.Equal("SdCard", device.Type);
        }

        /// <summary>
        /// Test: Find non-existent device returns null
        /// </summary>
        [Fact]
        public void FindDeviceById_NotFound_ReturnsNull()
        {
            // Arrange
            _devices.Add(new DeviceSignature { Id = "DEVICE_001", Type = "Mtp", Name = "Camera 1" });

            // Act
            var device = _devices.FirstOrDefault(d => d.Id == "NONEXISTENT");

            // Assert
            Assert.Null(device);
        }

        /// <summary>
        /// Test: Get device count
        /// </summary>
        [Fact]
        public void GetDeviceCount_ReturnsCorrectCount()
        {
            // Arrange
            _devices.Add(new DeviceSignature { Id = "DEVICE_001", Type = "Mtp", Name = "Camera 1" });
            _devices.Add(new DeviceSignature { Id = "DEVICE_002", Type = "SdCard", Name = "SD Card" });

            // Act
            var count = _devices.Count;

            // Assert
            Assert.Equal(2, count);
        }

        /// <summary>
        /// Test: Device list maintains order
        /// </summary>
        [Fact]
        public void DeviceList_MaintainsOrder()
        {
            // Arrange
            var dev1 = new DeviceSignature { Id = "DEVICE_001", Type = "Mtp", Name = "First" };
            var dev2 = new DeviceSignature { Id = "DEVICE_002", Type = "SdCard", Name = "Second" };
            var dev3 = new DeviceSignature { Id = "DEVICE_003", Type = "Mtp", Name = "Third" };

            _devices.Add(dev1);
            _devices.Add(dev2);
            _devices.Add(dev3);

            // Act & Assert
            Assert.Equal("First", _devices[0].Name);
            Assert.Equal("Second", _devices[1].Name);
            Assert.Equal("Third", _devices[2].Name);
        }

        /// <summary>
        /// Test: Duplicate device IDs are handled (remove all matching)
        /// </summary>
        [Fact]
        public void RemoveDuplicateDeviceIds_Success()
        {
            // Arrange
            _devices.Add(new DeviceSignature { Id = "DEVICE_001", Type = "Mtp", Name = "Camera 1" });
            _devices.Add(new DeviceSignature { Id = "DEVICE_001", Type = "Mtp", Name = "Camera 1 Duplicate" });
            _devices.Add(new DeviceSignature { Id = "DEVICE_002", Type = "SdCard", Name = "SD Card" });

            // Act
            var removed = _devices.RemoveAll(d => d.Id == "DEVICE_001");

            // Assert
            Assert.Equal(2, removed); // Both duplicates removed
            Assert.Single(_devices);
            Assert.Equal("DEVICE_002", _devices[0].Id);
        }

        /// <summary>
        /// Test: Device list iteration works correctly
        /// </summary>
        [Fact]
        public void IterateDevices_Success()
        {
            // Arrange
            _devices.Add(new DeviceSignature { Id = "DEVICE_001", Type = "Mtp", Name = "Camera 1" });
            _devices.Add(new DeviceSignature { Id = "DEVICE_002", Type = "SdCard", Name = "SD Card" });
            _devices.Add(new DeviceSignature { Id = "DEVICE_003", Type = "Mtp", Name = "Camera 2" });

            // Act
            var names = new List<string>();
            foreach (var device in _devices)
            {
                names.Add(device.Name);
            }

            // Assert
            Assert.Equal(3, names.Count);
            Assert.Contains("Camera 1", names);
            Assert.Contains("SD Card", names);
            Assert.Contains("Camera 2", names);
        }
    }
}
