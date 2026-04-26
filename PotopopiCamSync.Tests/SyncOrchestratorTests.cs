using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PotopopiCamSync.Models;
using PotopopiCamSync.Services;

namespace PotopopiCamSync.Tests;

/// <summary>
/// Tests for SyncOrchestrator using a mock IDeviceProvider.
/// Validates two-stage pipeline, duplicate-skip, and early-exit when no local folder is set.
/// </summary>
public class SyncOrchestratorTests : IDisposable
{
    private readonly string _localFolder;
    private readonly string _settingsFolder;
    private readonly SettingsService _settings;
    private readonly SyncOrchestrator _orchestrator;
    private readonly List<string> _progressMessages = new();

    public SyncOrchestratorTests()
    {
        _localFolder = Path.Combine(Path.GetTempPath(), $"OrchestratorTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_localFolder);

        _settingsFolder = Path.Combine(Path.GetTempPath(), $"SettingsTest_{Guid.NewGuid()}");
        _settings = new SettingsService(_settingsFolder);
        
        _settings.Config.LocalBackupFolder = _localFolder;
        _settings.Config.EnableImmichSync = false;

        _orchestrator = new SyncOrchestrator(_settings, NullLogger<SyncOrchestrator>.Instance);
        _orchestrator.OnSyncProgress += msg => _progressMessages.Add(msg);
    }

    private Mock<IDeviceProvider> MakeMockDevice(string deviceId = "dev-001", List<SyncFile>? files = null)
    {
        var mock = new Mock<IDeviceProvider>();
        mock.Setup(d => d.DeviceId).Returns(deviceId);
        mock.Setup(d => d.DeviceName).Returns("Mock Camera");
        mock.Setup(d => d.IsConnected).Returns(true);
        mock.Setup(d => d.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mock.Setup(d => d.GetFilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(files ?? new List<SyncFile>());

        mock.Setup(d => d.DownloadToStreamAsync(It.IsAny<SyncFile>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(async (SyncFile f, Stream dest, CancellationToken ct) =>
            {
                byte[] data = System.Text.Encoding.UTF8.GetBytes("FAKEDATA");
                await dest.WriteAsync(data, ct);
            });

        return mock;
    }

    private SyncFile MakeSyncFile(string name)
    {
        return new SyncFile
        {
            FileName = name,
            OriginalPath = $"/camera/DCIM/{name}",
            RelativePath = name,
            Size = 8, 
            CreationTime = new DateTime(2026, 4, 26)
        };
    }

    [Fact]
    public async Task StartSyncAsync_Exits_Early_When_No_LocalFolder()
    {
        _settings.Config.LocalBackupFolder = "";
        var mock = MakeMockDevice();

        await _orchestrator.StartSyncAsync(mock.Object);

        Assert.Contains(_progressMessages, m => m.Contains("not configured"));
        mock.Verify(d => d.ConnectAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartSyncAsync_Downloads_File_To_LocalFolder()
    {
        var file = MakeSyncFile("IMG_001.jpg");
        var mock = MakeMockDevice(files: new List<SyncFile> { file });

        await _orchestrator.StartSyncAsync(mock.Object);

        string expectedPath = Path.Combine(_localFolder, "2026-04-26", "IMG_001.jpg");
        Assert.True(File.Exists(expectedPath));
        Assert.Equal("FAKEDATA", File.ReadAllText(expectedPath));
    }

    [Fact]
    public async Task StartSyncAsync_Skips_Already_Synced_Files()
    {
        var file = MakeSyncFile("IMG_002.jpg");
        string fileId = file.GetIdentifier();

        _settings.State.SyncedFiles["dev-001"] = new HashSet<string> { fileId };

        var mock = MakeMockDevice(files: new List<SyncFile> { file });

        await _orchestrator.StartSyncAsync(mock.Object);

        mock.Verify(d => d.DownloadToStreamAsync(It.IsAny<SyncFile>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartSyncAsync_Marks_File_As_Synced_After_Download()
    {
        var file = MakeSyncFile("IMG_003.jpg");
        var mock = MakeMockDevice(files: new List<SyncFile> { file });

        await _orchestrator.StartSyncAsync(mock.Object);

        bool wasSynced = _settings.State.SyncedFiles.ContainsKey("dev-001") &&
                         _settings.State.SyncedFiles["dev-001"].Contains(file.GetIdentifier());
        Assert.True(wasSynced);
    }

    [Fact]
    public async Task StartSyncAsync_Calls_Disconnect_After_Sync()
    {
        var mock = MakeMockDevice();

        await _orchestrator.StartSyncAsync(mock.Object);

        mock.Verify(d => d.Disconnect(), Times.Once);
    }

    [Fact]
    public async Task StartSyncAsync_Respects_CancellationToken()
    {
        var files = new List<SyncFile> { MakeSyncFile("IMG_004.jpg"), MakeSyncFile("IMG_005.jpg") };
        var mock = MakeMockDevice(files: files);

        using var cts = new CancellationTokenSource();

        mock.Setup(d => d.DownloadToStreamAsync(It.IsAny<SyncFile>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(async (SyncFile f, Stream dest, CancellationToken ct) =>
            {
                cts.Cancel();
                await Task.Delay(10, ct); 
            });

        await _orchestrator.StartSyncAsync(mock.Object, cts.Token);

        Assert.Contains(_progressMessages, m => m.Contains("cancel", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SyncLocalToImmichAsync_Reports_Not_Configured_Without_Immich()
    {
        _settings.Config.EnableImmichSync = false;

        await _orchestrator.SyncLocalToImmichAsync();

        Assert.Contains(_progressMessages, m => m.Contains("not configured", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (Directory.Exists(_localFolder))
            Directory.Delete(_localFolder, recursive: true);
        if (Directory.Exists(_settingsFolder))
            Directory.Delete(_settingsFolder, recursive: true);
    }
}
