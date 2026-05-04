using System.IO;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.Utilities
{
    public static class FileUtilities
    {
        public static bool IsSupportedMedia(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return false;
            string ext = Path.GetExtension(fileName);
            return FileConstants.SupportedMediaExtensions.Contains(ext);
        }

        public static bool IsImage(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return false;
            string ext = Path.GetExtension(fileName);
            return FileConstants.ImageExtensions.Contains(ext);
        }
    }
}
