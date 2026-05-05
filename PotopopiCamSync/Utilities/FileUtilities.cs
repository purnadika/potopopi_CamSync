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

        public static string GetMimeType(string fileName)
        {
            string ext = Path.GetExtension(fileName).ToUpperInvariant();
            return ext switch
            {
                ".JPG" or ".JPEG" => "image/jpeg",
                ".PNG"            => "image/png",
                ".HEIC"           => "image/heic",
                ".CR2" or ".CR3"  => "image/x-canon-cr2",
                ".NEF"            => "image/x-nikon-nef",
                ".ARW"            => "image/x-sony-arw",
                ".DNG"            => "image/x-adobe-dng",
                ".ORF"            => "image/x-olympus-orf",
                ".MP4"            => "video/mp4",
                ".MOV"            => "video/quicktime",
                ".AVI"            => "video/x-msvideo",
                _                 => "application/octet-stream"
            };
        }
    }
}
