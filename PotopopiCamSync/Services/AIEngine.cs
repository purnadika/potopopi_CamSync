using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace PotopopiCamSync.Services
{
    public class AIEngine : IMediaAnalyzer
    {
        private readonly ILogger<AIEngine> _logger;

        public AIEngine(ILogger<AIEngine> logger)
        {
            _logger = logger;
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

                using var laplacian = new Mat();
                Cv2.Laplacian(src, laplacian, MatType.CV_64F);

                Cv2.MeanStdDev(laplacian, out _, out var stdDev);
                double variance = stdDev.Val0 * stdDev.Val0;

                return variance;
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

        public async Task<AnalysisResult> AnalyzeAsync(string filePath, CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                double blurScore = GetBlurScore(filePath);
                ulong hash = GetImageHash(filePath);

                return new AnalysisResult
                {
                    BlurScore = blurScore,
                    ImageHash = hash,
                    IsPotentiallyBlurry = blurScore < 100, // Threshold
                    Tags = new System.Collections.Generic.List<string>()
                };
            }, ct);
        }
    }

    public class AnalysisResult
    {
        public double BlurScore { get; set; }
        public ulong ImageHash { get; set; }
        public bool IsPotentiallyBlurry { get; set; }
        public System.Collections.Generic.List<string> Tags { get; set; } = new();
    }
}
