using System;
using System.IO;

namespace PotopopiCamSync.Models
{
    public class SyncFile
    {
        public string RelativePath { get; set; } = string.Empty;
        public string OriginalPath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime CreationTime { get; set; }
        
        public string GetIdentifier()
        {
            // A simple identifier. Ideally we hash the file, but Size + Name is fast and mostly reliable for photos.
            return $"{FileName}_{Size}";
        }
    }
}
