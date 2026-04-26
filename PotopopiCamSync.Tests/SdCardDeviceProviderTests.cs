using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using PotopopiCamSync.Models;
using PotopopiCamSync.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace PotopopiCamSync.Tests;

/// <summary>
/// Tests for SdCardDeviceProvider file scanning logic using a real temp directory.
/// </summary>
public class SdCardDeviceProviderTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _dcimPath;

    public SdCardDeviceProviderTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"PotopopiTests_{Guid.NewGuid()}");
        _dcimPath = Path.Combine(_tempRoot, "DCIM");
        Directory.CreateDirectory(_dcimPath);
    }

    private void CreateFile(string relativePath)
    {
        string full = Path.Combine(_dcimPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "fake content");
    }

    [Fact]
    public async Task GetFilesAsync_Returns_SupportedExtensions_Only()
    {
        CreateFile(@"100CANON\IMG_001.jpg");
        CreateFile(@"100CANON\IMG_002.CR2");
        CreateFile(@"100CANON\THUMBS.db");    // should be excluded
        CreateFile(@"100CANON\video.mp4");
        CreateFile(@"100CANON\readme.txt");   // should be excluded

        var provider = new SdCardDeviceProvider("vol123", "SD Card", _tempRoot + "\\");
        var files = await provider.GetFilesAsync();

        Assert.Equal(3, files.Count);
        Assert.All(files, f => Assert.DoesNotContain(".db", f.FileName, StringComparison.OrdinalIgnoreCase));
        Assert.All(files, f => Assert.DoesNotContain(".txt", f.FileName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetFilesAsync_Traverses_SubDirectories()
    {
        CreateFile(@"100CANON\IMG_001.jpg");
        CreateFile(@"101CANON\IMG_002.jpg");
        CreateFile(@"101CANON\sub\IMG_003.CR2");

        var provider = new SdCardDeviceProvider("vol123", "SD Card", _tempRoot + "\\");
        var files = await provider.GetFilesAsync();

        Assert.Equal(3, files.Count);
    }

    [Fact]
    public async Task GetFilesAsync_Returns_Empty_When_No_DCIM()
    {
        var emptyRoot = Path.Combine(Path.GetTempPath(), $"NoDecim_{Guid.NewGuid()}");
        Directory.CreateDirectory(emptyRoot);

        var provider = new SdCardDeviceProvider("vol999", "Empty Drive", emptyRoot + "\\");
        var files = await provider.GetFilesAsync();

        Assert.Empty(files);
        Directory.Delete(emptyRoot);
    }

    [Fact]
    public async Task GetFilesAsync_Respects_CancellationToken()
    {
        for (int i = 0; i < 50; i++)
            CreateFile($@"100CANON\IMG_{i:000}.jpg");

        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        var provider = new SdCardDeviceProvider("vol123", "SD Card", _tempRoot + "\\");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.GetFilesAsync(cts.Token));
    }

    [Fact]
    public async Task DownloadToStreamAsync_Copies_File_Contents()
    {
        string content = "HELLO JPEG DATA";
        CreateFile(@"100CANON\test.jpg");
        File.WriteAllText(Path.Combine(_dcimPath, @"100CANON\test.jpg"), content);

        var provider = new SdCardDeviceProvider("vol123", "SD Card", _tempRoot + "\\");
        var files = await provider.GetFilesAsync();
        Assert.Single(files);

        using var ms = new MemoryStream();
        await provider.DownloadToStreamAsync(files[0], ms);
        ms.Position = 0;
        string read = new StreamReader(ms).ReadToEnd();

        Assert.Equal(content, read);
    }

    [Fact]
    public async Task DeleteFileAsync_Removes_File()
    {
        CreateFile(@"100CANON\delete_me.jpg");
        string fullPath = Path.Combine(_dcimPath, @"100CANON\delete_me.jpg");
        Assert.True(File.Exists(fullPath));

        var provider = new SdCardDeviceProvider("vol123", "SD Card", _tempRoot + "\\");
        var files = await provider.GetFilesAsync();
        Assert.Single(files);

        await provider.DeleteFileAsync(files[0]);
        Assert.False(File.Exists(fullPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
