using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.Services
{
    public class SdCardDeviceProvider : IDeviceProvider
    {
        private string _drivePath;
        
        public string DeviceId { get; private set; }
        public string DeviceName { get; private set; }
        public bool IsConnected => Directory.Exists(_drivePath);

        public SdCardDeviceProvider(string deviceId, string deviceName, string drivePath)
        {
            DeviceId = deviceId;
            DeviceName = deviceName;
            _drivePath = drivePath;
        }

        public Task ConnectAsync()
        {
            return Task.CompletedTask;
        }

        public void Disconnect()
        {
        }

        public Task<List<SyncFile>> GetFilesAsync()
        {
            return Task.Run(() =>
            {
                var files = new List<SyncFile>();
                if (!IsConnected) return files;

                string dcimPath = Path.Combine(_drivePath, "DCIM");
                if (Directory.Exists(dcimPath))
                {
                    FindFilesRecursive(dcimPath, files, dcimPath);
                }
                return files;
            });
        }

        private void FindFilesRecursive(string path, List<SyncFile> files, string basePath)
        {
            try
            {
                var fileItems = Directory.GetFiles(path);
                foreach (var file in fileItems)
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (ext == ".jpg" || ext == ".jpeg" || ext == ".cr2" || ext == ".mp4" || ext == ".mov")
                    {
                        var fileInfo = new FileInfo(file);
                        files.Add(new SyncFile
                        {
                            OriginalPath = file,
                            RelativePath = file.Substring(basePath.Length).TrimStart('\\', '/'),
                            FileName = Path.GetFileName(file),
                            Size = fileInfo.Length,
                            CreationTime = fileInfo.CreationTime
                        });
                    }
                }

                var directories = Directory.GetDirectories(path);
                foreach (var dir in directories)
                {
                    FindFilesRecursive(dir, files, basePath);
                }
            }
            catch { /* Ignore access issues */ }
        }

        public Task<Stream> GetFileStreamAsync(SyncFile file)
        {
            return Task.Run(() =>
            {
                return (Stream)File.OpenRead(file.OriginalPath);
            });
        }

        public Task DeleteFileAsync(SyncFile file)
        {
            return Task.Run(() =>
            {
                if (File.Exists(file.OriginalPath))
                {
                    File.Delete(file.OriginalPath);
                }
            });
        }
    }
}
