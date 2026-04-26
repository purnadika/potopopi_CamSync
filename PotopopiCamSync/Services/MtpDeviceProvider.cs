using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MediaDevices;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.Services
{
    public class MtpDeviceProvider : IDeviceProvider
    {
        private MediaDevice _device;
        public string DeviceId { get; private set; }
        public string DeviceName { get; private set; }
        public bool IsConnected => _device != null && _device.IsConnected;

        public MtpDeviceProvider(string deviceId, string deviceName)
        {
            DeviceId = deviceId;
            DeviceName = deviceName;
        }

        public Task ConnectAsync()
        {
            return Task.Run(() =>
            {
                var devices = MediaDevice.GetDevices();
                _device = devices.FirstOrDefault(d => d.DeviceId == DeviceId || d.FriendlyName == DeviceName);
                if (_device != null)
                {
                    _device.Connect();
                }
                else
                {
                    throw new Exception("MTP Device not found or could not be connected.");
                }
            });
        }

        public void Disconnect()
        {
            if (_device != null && _device.IsConnected)
            {
                _device.Disconnect();
                _device.Dispose();
            }
        }

        public Task<List<SyncFile>> GetFilesAsync()
        {
            return Task.Run(() =>
            {
                var files = new List<SyncFile>();
                if (!IsConnected) return files;

                // Look for DCIM folder across all storage
                var storages = _device.GetDrives();
                foreach (var storage in storages)
                {
                    var dcimPath = _device.GetDirectories(storage.RootDirectory.FullName).FirstOrDefault(d => d.EndsWith("DCIM", StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(dcimPath))
                    {
                        FindFilesRecursive(dcimPath, files, dcimPath);
                    }
                }
                return files;
            });
        }

        private void FindFilesRecursive(string path, List<SyncFile> files, string basePath)
        {
            var directories = _device.GetDirectories(path);
            foreach (var dir in directories)
            {
                FindFilesRecursive(dir, files, basePath);
            }

            var fileItems = _device.GetFiles(path);
            foreach (var file in fileItems)
            {
                var fileInfo = _device.GetFileInfo(file);
                // Filter only image/video types if needed, or take all
                string ext = Path.GetExtension(file).ToLower();
                if (ext == ".jpg" || ext == ".jpeg" || ext == ".cr2" || ext == ".mp4" || ext == ".mov")
                {
                    files.Add(new SyncFile
                    {
                        OriginalPath = file,
                        RelativePath = file.Substring(basePath.Length).TrimStart('\\', '/'),
                        FileName = Path.GetFileName(file),
                        Size = (long)fileInfo.Length,
                        CreationTime = fileInfo.CreationTime ?? DateTime.Now
                    });
                }
            }
        }

        public Task<Stream> GetFileStreamAsync(SyncFile file)
        {
            return Task.Run(() =>
            {
                var memStream = new MemoryStream();
                _device.DownloadFile(file.OriginalPath, memStream);
                memStream.Position = 0;
                return (Stream)memStream;
            });
        }

        public Task DeleteFileAsync(SyncFile file)
        {
            return Task.Run(() =>
            {
                _device.DeleteFile(file.OriginalPath);
            });
        }
    }
}
