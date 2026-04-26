using System;
using System.IO;
using System.Threading.Tasks;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.Services
{
    public class LocalFolderSync : ISyncDestination
    {
        private readonly string _targetFolder;

        public LocalFolderSync(string targetFolder)
        {
            _targetFolder = targetFolder;
        }

        public async Task<bool> UploadAsync(SyncFile file, Stream fileStream)
        {
            try
            {
                if (string.IsNullOrEmpty(_targetFolder)) return false;

                if (!Directory.Exists(_targetFolder))
                {
                    Directory.CreateDirectory(_targetFolder);
                }

                // Create date-based folder structure like 2026-04-26
                string dateFolder = file.CreationTime.ToString("yyyy-MM-dd");
                string finalDirPath = Path.Combine(_targetFolder, dateFolder);
                if (!Directory.Exists(finalDirPath))
                {
                    Directory.CreateDirectory(finalDirPath);
                }

                string targetPath = Path.Combine(finalDirPath, file.FileName);
                
                // If file already exists locally with same size, skip writing
                if (File.Exists(targetPath))
                {
                    var fileInfo = new FileInfo(targetPath);
                    if (fileInfo.Length == file.Size)
                        return true;
                }

                using (var fileStreamOutput = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                {
                    fileStream.Position = 0;
                    await fileStream.CopyToAsync(fileStreamOutput);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
