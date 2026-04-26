using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PotopopiCamSync.Models;
using PotopopiCamSync.Services;

namespace PotopopiCamSync.Tests;

/// <summary>
/// Tests for LocalFolderSync — verifies date folder structure, size-skip logic, and async write.
/// </summary>
public class LocalFolderSyncTests : IDisposable
{
    private readonly string _targetFolder;
    private readonly ILogger<LocalFolderSync> _logger = NullLogger<LocalFolderSync>.Instance;

    public LocalFolderSyncTests()
    {
        _targetFolder = Path.Combine(Path.GetTempPath(), $"LocalSyncTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_targetFolder);
    }

    private SyncFile MakeSyncFile(string fileName, DateTime created, long size = 100)
    {
        return new SyncFile
        {
            FileName = fileName,
            OriginalPath = Path.Combine(Path.GetTempPath(), fileName),
            RelativePath = fileName,
            CreationTime = created,
            Size = size
        };
    }

    [Fact]
    public async Task UploadAsync_Creates_DateFolder_And_File()
    {
        var file = MakeSyncFile("test.jpg", new DateTime(2026, 4, 26));

        // Create a real source file to copy from
        string sourcePath = Path.Combine(Path.GetTempPath(), "test_source.jpg");
        File.WriteAllText(sourcePath, "fake jpeg");
        file.OriginalPath = sourcePath;

        var sync = new LocalFolderSync(_targetFolder, _logger);
        bool result = await sync.UploadAsync(file, sourcePath);

        Assert.True(result);
        string expectedPath = Path.Combine(_targetFolder, "2026-04-26", "test.jpg");
        Assert.True(File.Exists(expectedPath));

        File.Delete(sourcePath);
    }

    [Fact]
    public async Task UploadAsync_Skips_If_Same_Size_Already_Exists()
    {
        string sourcePath = Path.Combine(Path.GetTempPath(), "skip_test.jpg");
        File.WriteAllText(sourcePath, "data");
        long size = new FileInfo(sourcePath).Length;

        var file = MakeSyncFile("skip_test.jpg", new DateTime(2026, 4, 26), size);

        // Pre-create the destination file with same size
        string destDir = Path.Combine(_targetFolder, "2026-04-26");
        Directory.CreateDirectory(destDir);
        string destPath = Path.Combine(destDir, "skip_test.jpg");
        File.WriteAllText(destPath, "data"); // same content = same size

        var sync = new LocalFolderSync(_targetFolder, _logger);
        bool result = await sync.UploadAsync(file, sourcePath);

        Assert.True(result);
        // File should NOT have been rewritten — still original content
        Assert.Equal("data", File.ReadAllText(destPath));

        File.Delete(sourcePath);
    }

    [Fact]
    public async Task UploadAsync_Returns_False_When_SourceFile_Missing()
    {
        var file = MakeSyncFile("missing.jpg", DateTime.Now);
        var sync = new LocalFolderSync(_targetFolder, _logger);

        bool result = await sync.UploadAsync(file, "/nonexistent/path/missing.jpg");

        Assert.False(result);
    }

    [Fact]
    public void GetLocalBackupPath_Returns_Correct_Path()
    {
        var file = MakeSyncFile("IMG_001.CR2", new DateTime(2026, 1, 15));
        var sync = new LocalFolderSync(_targetFolder, _logger);

        string path = sync.GetLocalBackupPath(file);

        Assert.Equal(Path.Combine(_targetFolder, "2026-01-15", "IMG_001.CR2"), path);
    }

    [Fact]
    public async Task UploadAsync_Respects_CancellationToken()
    {
        string sourcePath = Path.Combine(Path.GetTempPath(), "cancel_test.jpg");
        File.WriteAllBytes(sourcePath, new byte[1024 * 1024]); // 1MB

        var file = MakeSyncFile("cancel_test.jpg", DateTime.Now, 1024 * 1024);
        var sync = new LocalFolderSync(_targetFolder, _logger);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sync.UploadAsync(file, sourcePath, cts.Token));

        File.Delete(sourcePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_targetFolder))
            Directory.Delete(_targetFolder, recursive: true);
    }
}
