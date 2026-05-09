using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using PotopopiCamSync.Models;
using PotopopiCamSync.Repositories;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace PotopopiCamSync.Services
{
    public class AIEngineService : IMediaAnalyzer
    {
        private readonly ILogger<AIEngineService> _logger;
        private readonly ISettingsRepository _settings;

        public AIEngineService(ILogger<AIEngineService> logger, ISettingsRepository settings)
        {
            _logger = logger;
            _settings = settings;
        }

        /// <summary>
        /// Detects if an image is blurry using the Laplacian variance method.
        /// Higher score = Sharper image.
        /// </summary>
        public double GetBlurScore(string filePath)
        {
            try
            {
                using var src = Cv2.ImRead(filePath, ImreadModes.Grayscale);
                if (src.Empty()) return 0;

                // For large images, resize to a reasonable processing size while maintaining detail
                // 1024px width is usually enough for blur detection
                using var processed = new Mat();
                if (src.Cols > 1200)
                {
                    double scale = 1200.0 / src.Cols;
                    Cv2.Resize(src, processed, new Size(0, 0), scale, scale, InterpolationFlags.Area);
                }
                else
                {
                    src.CopyTo(processed);
                }

                // Grid-based Laplacian Variance (helps with Bokeh/Portraits)
                // We split the image into 4x4 grid and take the MAX variance.
                // If any part of the image is sharp, the image is not considered blurry.
                int rows = 4;
                int cols = 4;
                int cellWidth = processed.Cols / cols;
                int cellHeight = processed.Rows / rows;
                double maxVariance = 0;

                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < cols; x++)
                    {
                        using var cell = new Mat(processed, new Rect(x * cellWidth, y * cellHeight, cellWidth, cellHeight));
                        using var laplacian = new Mat();
                        Cv2.Laplacian(cell, laplacian, MatType.CV_64F);

                        Cv2.MeanStdDev(laplacian, out _, out var stdDev);
                        double variance = stdDev.Val0 * stdDev.Val0;
                        if (variance > maxVariance) maxVariance = variance;
                    }
                }

                return maxVariance;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Blur detection failed for {File}: {Msg}", Path.GetFileName(filePath), ex.Message);
                return 1000; // Assume sharp if check fails
            }
        }

        /// <summary>
        /// Calculates a simple Difference Hash (dHash) for duplicate detection.
        /// </summary>
        public ulong GetImageHash(string filePath)
        {
            try
            {
                using var src = Cv2.ImRead(filePath, ImreadModes.Grayscale);
                if (src.Empty()) return 0;

                // Resize to 9x8 for dHash
                using var resized = new Mat();
                Cv2.Resize(src, resized, new Size(9, 8), 0, 0, InterpolationFlags.Area);

                ulong hash = 0;
                for (int i = 0; i < 8; i++)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        if (resized.At<byte>(i, j) > resized.At<byte>(i, j + 1))
                        {
                            hash |= (1UL << (i * 8 + j));
                        }
                    }
                }
                return hash;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Hashing failed for {File}: {Msg}", Path.GetFileName(filePath), ex.Message);
                return 0;
            }
        }

        private double? GetAperture(string filePath)
        {
            try
            {
                var directories = ImageMetadataReader.ReadMetadata(filePath);
                var subIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                if (subIfdDirectory != null && subIfdDirectory.TryGetDouble(ExifDirectoryBase.TagAperture, out double aperture))
                {
                    return aperture;
                }
                
                // Fallback to F-Number if Aperture tag is not found
                if (subIfdDirectory != null && subIfdDirectory.TryGetDouble(ExifDirectoryBase.TagFNumber, out double fNumber))
                {
                    return fNumber;
                }
            }
            catch { /* Ignore EXIF errors */ }
            return null;
        }

        public async Task<AnalysisResultModel> AnalyzeAsync(string filePath, CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                double blurScore = GetBlurScore(filePath);
                ulong hash = GetImageHash(filePath);
                double? aperture = GetAperture(filePath);

                double threshold = _settings.Config.AIBlurThreshold;

                // Adjust threshold based on aperture (bokeh handling)
                if (aperture.HasValue)
                {
                    if (aperture.Value <= 2.0)
                    {
                        threshold *= 0.4; // Very lenient for wide open aperture
                        _logger.LogDebug("Wide aperture detected (f/{F:F1}), adjusting blur threshold to {T:F1}", aperture.Value, threshold);
                    }
                    else if (aperture.Value <= 4.0)
                    {
                        threshold *= 0.6; // Moderately lenient
                        _logger.LogDebug("Moderate aperture detected (f/{F:F1}), adjusting blur threshold to {T:F1}", aperture.Value, threshold);
                    }
                }

                return new AnalysisResultModel
                {
                    BlurScore = blurScore,
                    ImageHash = hash,
                    IsPotentiallyBlurry = blurScore < threshold,
                    Tags = aperture.HasValue ? new System.Collections.Generic.List<string> { $"f/{aperture.Value:F1}" } : new System.Collections.Generic.List<string>()
                };
            }, ct);
        }
    }
}
