using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;
using PotopopiCamSync.Services;
using Xunit;

namespace PotopopiCamSync.Tests
{
    public class AIEngineTests : IDisposable
    {
        private readonly AIEngine _aiEngine;
        private readonly string _tempDir;

        public AIEngineTests()
        {
            _aiEngine = new AIEngine(NullLogger<AIEngine>.Instance);
            _tempDir = Path.Combine(Path.GetTempPath(), "PotopopiAITests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        private string CreateTestImage(bool blurry)
        {
            string path = Path.Combine(_tempDir, $"test_{(blurry ? "blur" : "sharp")}.jpg");
            using var mat = new Mat(100, 100, MatType.CV_8UC3, Scalar.White);
            
            // Draw some sharp lines
            Cv2.Line(mat, new Point(10, 10), new Point(90, 90), Scalar.Black, 2);
            Cv2.Line(mat, new Point(10, 90), new Point(90, 10), Scalar.Black, 2);

            if (blurry)
            {
                // Apply heavy Gaussian blur
                Cv2.GaussianBlur(mat, mat, new Size(15, 15), 0);
            }

            mat.SaveImage(path);
            return path;
        }

        [Fact]
        public async Task AnalyzeAsync_ShouldIdentifyBlurryImage()
        {
            // Arrange
            string sharpPath = CreateTestImage(false);
            string blurryPath = CreateTestImage(true);

            // Act
            var sharpResult = await _aiEngine.AnalyzeAsync(sharpPath);
            var blurryResult = await _aiEngine.AnalyzeAsync(blurryPath);

            // Assert
            Assert.False(sharpResult.IsPotentiallyBlurry);
            Assert.True(blurryResult.IsPotentiallyBlurry);
            Assert.True(sharpResult.BlurScore > blurryResult.BlurScore);
        }

        [Fact]
        public void GetImageHash_ShouldBeSameForIdenticalImages()
        {
            // Arrange
            string path1 = CreateTestImage(false);
            string path2 = Path.Combine(_tempDir, "test_copy.jpg");
            File.Copy(path1, path2);

            // Act
            ulong hash1 = _aiEngine.GetImageHash(path1);
            ulong hash2 = _aiEngine.GetImageHash(path2);

            // Assert
            Assert.Equal(hash1, hash2);
            Assert.NotEqual(0UL, hash1);
        }
    }
}
