using Xunit;
using PotopopiCamSync.Services;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.Tests
{
    public class FileFilterTests
    {
        [Theory]
        [InlineData("test.jpg", false)]
        [InlineData("test.cr2", true)]
        [InlineData("test.CR3", true)]
        [InlineData("test.NEF", true)]
        [InlineData("test.ARW", true)]
        [InlineData("test.DNG", true)]
        [InlineData("test.ORF", true)]
        public void ShouldExcludeRawFiles_WhenEnabled(string fileName, bool expected)
        {
            // Arrange
            var config = new AppConfigModel { ExcludeRawFiles = true };
            var filter = new FileFilter(config);

            // Act
            bool actual = filter.ShouldExclude(fileName);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("test.txt", "*.txt", true)]
        [InlineData("test.log", "*.txt", false)]
        [InlineData("important.doc", "*.txt,*.doc", true)]
        public void ShouldExcludeByCustomPatterns(string fileName, string patterns, bool expected)
        {
            // Arrange
            var config = new AppConfigModel { ImmichExclusionPatterns = patterns };
            var filter = new FileFilter(config);

            // Act
            bool actual = filter.ShouldExclude(fileName);

            // Assert
            Assert.Equal(expected, actual);
        }
    }
}
