using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.Services
{
    /// <summary>
    /// Filters files based on glob patterns.
    /// Supports *, ?, and exact matches.
    /// </summary>
    public class FileFilter
    {
        private readonly bool _excludeRaw;
        private static readonly string[] RawExtensions = { ".cr2", ".cr3", ".nef", ".arw", ".dng", ".orf" };
        private readonly List<Regex> _patterns;

        public FileFilter(string globPatterns) : this(new AppConfigModel { ImmichExclusionPatterns = globPatterns })
        {
        }

        public FileFilter(AppConfigModel config)
        {
            _excludeRaw = config.ExcludeRawFiles;
            _patterns = new List<Regex>();

            if (string.IsNullOrWhiteSpace(config.ImmichExclusionPatterns))
                return;

            var patterns = config.ImmichExclusionPatterns.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var pattern in patterns)
            {
                var trimmed = pattern.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    _patterns.Add(GlobToRegex(trimmed));
                }
            }
        }

        /// <summary>
        /// Returns true if file should be EXCLUDED.
        /// </summary>
        public bool ShouldExclude(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            string ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            if (_excludeRaw && RawExtensions.Contains(ext))
                return true;

            string name = System.IO.Path.GetFileName(fileName).ToLowerInvariant();
            return _patterns.Any(p => p.IsMatch(name));
        }

        /// <summary>
        /// Convert glob pattern to regex.
        /// Examples:
        ///   *.tmp → matches any .tmp file
        ///   Thumbs.db → exact match
        ///   ?.bak → matches single char + .bak
        /// </summary>
        private static Regex GlobToRegex(string glob)
        {
            var pattern = glob.ToLowerInvariant();

            // Escape regex special chars except * and ?
            pattern = Regex.Escape(pattern);

            // Convert glob wildcards back to regex
            pattern = pattern.Replace("\\*", ".*");  // * → .*
            pattern = pattern.Replace("\\?", ".");   // ? → .

            return new Regex($"^{pattern}$", RegexOptions.IgnoreCase);
        }
    }
}
