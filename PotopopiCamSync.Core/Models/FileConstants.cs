using System;
using System.Collections.Generic;

namespace PotopopiCamSync.Models
{
    public static class FileConstants
    {
        public static readonly HashSet<string> SupportedMediaExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".cr2", ".cr3", ".nef", ".arw", ".dng", ".png", ".heic", ".mp4", ".mov", ".avi"
        };

        public static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".heic", ".cr2", ".cr3", ".nef", ".arw", ".dng"
        };

    }
}
