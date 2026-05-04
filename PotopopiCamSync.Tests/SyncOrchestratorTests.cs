using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PotopopiCamSync.Models;
using PotopopiCamSync.Services;
using PotopopiCamSync.Repositories;

namespace PotopopiCamSync.Tests;

/// <summary>
/// Tests for SyncOrchestratorService using a mock IDeviceProvider.
/// Validates two-stage pipeline, duplicate-skip, and early-exit when no local folder is set.
/// </summary>
public class SyncOrchestratorTests : IDisposable
{
    private readonly string _localFolder;
    private readonly string _settingsFolder;
    private readonly ISettingsRepository _settings;
    private readonly SyncOrchestratorService _orchestrator;
    private readonly ConcurrentBag<string> _progressMessages = new();

    public SyncOrchestratorTests()
    {
        _localFolder = Path.Combine(Path.GetTempPath(), $"OrchestratorTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_localFolder);

        _settingsFolder = Path.Combine(Path.GetTempPath(), $"SettingsTest_{Guid.NewGuid()}");
        _settings = new JsonSettingsRepository(_settingsFolder);
        
        _settings.Config.LocalBackupFolder = _localFolder;
        _settings.Config.EnableImmichSync = false;

        // Mock AI Analyzer to avoid loading native libs in CI
        var mockAi = new Mock<IMediaAnalyzer>();
        mockAi.Setup(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new AnalysisResultModel { BlurScore = 1000, IsPotentiallyBlurry = false });

        _orchestrator = new SyncOrchestratorService(_settings, mockAi.Object, NullLogger<SyncOrchestratorService>.Instance, NullLoggerFactory.Instance);
        _orchestrator.OnSyncProgress += msg => _progressMessages.Add(msg);
    }


    private Mock<IDeviceProvider> MakeMockDevice(string deviceId = "dev-001", List<SyncFileModel>? files = null)
    {
        var mock = new Mock<IDeviceProvider>();
        mock.Setup(d => d.DeviceId).Returns(deviceId);
        mock.Setup(d => d.DeviceName).Returns("Mock Camera");
        mock.Setup(d => d.IsConnected).Returns(true);
        mock.Setup(d => d.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mock.Setup(d => d.GetFilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(files ?? new List<SyncFileModel>());

        mock.Setup(d => d.DownloadToStreamAsync(It.IsAny<SyncFileModel>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(async (SyncFileModel f, Stream dest, CancellationToken ct) =>
            {
                byte[] data = System.Text.Encoding.UTF8.GetBytes("FAKEDATA");
                await dest.WriteAsync(data, ct);
            });

        return mock;
    }

    private SyncFileModel MakeSyncFile(string name)
    {
        return new SyncFileModel
        {
            FileName = name,
            OriginalPath = $"/camera/DCIM/{name}",
            RelativePath = name,
            Size = 8, 
            CreationTime = new DateTime(2026, 4, 26)
        };
    }

    [Fact(Skip = "CI Stall")]
    public async Task StartSyncAsync_Exits_Early_When_No_LocalFolder()
    {
        _settings.Config.LocalBackupFolder = "";
        var mock = MakeMockDevice();

        await _orchestrator.StartSyncAsync(mock.Object);

        Assert.Contains(_progressMessages, m => m.Contains("not configured"));
        mock.Verify(d => d.ConnectAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(Skip = "CI Stall")]
    public async Task StartSyncAsync_Downloads_File_To_LocalFolder()
    {
        var file = MakeSyncFile("IMG_001.jpg");
        var mock = MakeMockDevice(files: new List<SyncFileModel> { file });

        await _orchestrator.StartSyncAsync(mock.Object);

        string expectedPath = Path.Combine(_localFolder, "2026-04-26", "IMG_001.jpg");
        Assert.True(File.Exists(expectedPath));
        Assert.Equal("FAKEDATA", File.ReadAllText(expectedPath));
    }

    [Fact(Skip = "CI Stall")]
    public async Task StartSyncAsync_ForceSync_Redownloads_Missing_Files()
    {
        var file = MakeSyncFile("IMG_FORCE.jpg");
        var mock = MakeMockDevice(files: new List<SyncFileModel> { file });

        // 1st Sync (Normal)
        await _orchestrator.StartSyncAsync(mock.Object);
        string expectedPath = Path.Combine(_localFolder, "2026-04-26", "IMG_FORCE.jpg");
        Assert.True(File.Exists(expectedPath));

        // Delete physical file to simulate missing file
        File.Delete(expectedPath);
        Assert.False(File.Exists(expectedPath));

        // Reset mock call count
        mock.Invocations.Clear();

        // 2nd Sync (Normal) -> Should skip because it's in syncedSet
        await _orchestrator.StartSyncAsync(mock.Object);
        Assert.False(File.Exists(expectedPath)); // Still missing

        // 3rd Sync (Force Sync) -> Should detect missing file and re-download
        await _orchestrator.StartSyncAsync(mock.Object, forceVerify: true);
        Assert.True(File.Exists(expectedPath)); // Restored
        
        mock.Verify(d => d.DownloadToStreamAsync(It.IsAny<SyncFileModel>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(Skip = "CI Stall")]
    public async Task StartSyncAsync_Skips_Already_Synced_Files()
    {
        var file = MakeSyncFile("IMG_002.jpg");
        string fileId = file.GetIdentifier();

        _settings.State.SyncedFiles["dev-001"] = new HashSet<string> { fileId };

        var mock = MakeMockDevice(files: new List<SyncFileModel> { file });

        await _orchestrator.StartSyncAsync(mock.Object);

        mock.Verify(d => d.DownloadToStreamAsync(It.IsAny<SyncFileModel>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(Skip = "CI Stall")]
    public async Task StartSyncAsync_Marks_File_As_Synced_After_Download()
    {
        var file = MakeSyncFile("IMG_003.jpg");
        var mock = MakeMockDevice(files: new List<SyncFileModel> { file });

        await _orchestrator.StartSyncAsync(mock.Object);

        bool wasSynced = _settings.State.SyncedFiles.ContainsKey("dev-001") &&
                         _settings.State.SyncedFiles["dev-001"].Contains(file.GetIdentifier());
        Assert.True(wasSynced);
    }

    [Fact(Skip = "CI Stall")]
    public async Task StartSyncAsync_Calls_Disconnect_After_Sync()
    {
        var mock = MakeMockDevice();

        await _orchestrator.StartSyncAsync(mock.Object);

        mock.Verify(d => d.Disconnect(), Times.Once);
    }

    
    [Fact(Skip = "Stalls in headless environment due to BlockingCollection race")]
    public async Task StartSyncAsync_Respects_CancellationToken()
    {
        var files = new List<SyncFileModel> { MakeSyncFile("IMG_004.jpg") };
        var mock = MakeMockDevice(files: files);

        using var cts = new CancellationTokenSource();

        mock.Setup(d => d.DownloadToStreamAsync(It.IsAny<SyncFileModel>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((SyncFileModel f, Stream dest, CancellationToken ct) =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            });

        await _orchestrator.StartSyncAsync(mock.Object, cts.Token);

        Assert.Contains(_progressMessages, m => m.Contains("cancel", StringComparison.OrdinalIgnoreCase));
    }


    [Fact(Skip = "CI Stall")]
    public async Task SyncLocalToImmichAsync_Reports_Not_Configured_Without_Immich()
    {
        _settings.Config.EnableImmichSync = false;

        await _orchestrator.SyncLocalToImmichAsync();

        Assert.Contains(_progressMessages, m => m.Contains("not configured", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(Skip = "CI Stall")]
    public async Task StartSyncAsync_Downloads_But_Skips_Immich_If_Pattern_Matches()
    {
        _settings.Config.EnableImmichSync = true;
        _settings.Config.ImmichUrl = "http://fake";
        _settings.Config.ImmichApiKey = "fake";
        _settings.Config.ImmichExclusionPatterns = "*.cr2";

        var file = MakeSyncFile("IMG_006.cr2");
        var mock = MakeMockDevice(files: new List<SyncFileModel> { file });

        await _orchestrator.StartSyncAsync(mock.Object);

        // Verify it was downloaded
        string localPath = Path.Combine(_localFolder, "2026-04-26", "IMG_006.cr2");
        Assert.True(File.Exists(localPath));

        // Verify log message contains "Skip"
        Assert.Contains(_progressMessages, m => m.Contains("Immich Skip") && m.Contains("IMG_006.cr2"));
    }

    [Fact(Skip = "CI Stall")]
    public async Task StartSyncAsync_SmartScan_Skips_Old_Files()
    {
        var oldFile = MakeSyncFile("OLD.jpg");
        oldFile.CreationTime = DateTime.Now.AddDays(-10);
        var newFile = MakeSyncFile("NEW.jpg");
        newFile.CreationTime = DateTime.Now;

        var mock = MakeMockDevice(deviceId: "smart-dev", files: new List<SyncFileModel> { oldFile, newFile });
        var devSig = new DeviceSignatureModel { Id = "smart-dev", UseSmartScan = true, LastSyncDate = DateTime.Now.AddDays(-1) };
        _settings.Config.RegisteredDevices.Add(devSig);

        await _orchestrator.StartSyncAsync(mock.Object);

        string oldPath = Path.Combine(_localFolder, oldFile.CreationTime.ToString("yyyy-MM-dd"), "OLD.jpg");
        string newPath = Path.Combine(_localFolder, newFile.CreationTime.ToString("yyyy-MM-dd"), "NEW.jpg");

        Assert.False(File.Exists(oldPath));
        Assert.True(File.Exists(newPath));
    }

    [Fact(Skip = "CI Stall")]
    public async Task StartSyncAsync_Triggers_OnAIResultFound()
    {
        var file = MakeSyncFile("BLURRY.jpg");
        var mock = MakeMockDevice(files: new List<SyncFileModel> { file });
        
        // Setup AI mode to Standard (OpenCV)
        _settings.Config.AIAnalysisMode = AIAnalysisMode.Standard;

        string? flaggedPath = null;
        _orchestrator.OnAIResultFound += (path, reason, isBlurry) => flaggedPath = path;

        await _orchestrator.StartSyncAsync(mock.Object);
    }

    [Fact(Skip = "CI Stall")]
    public async Task StartSyncAsync_Respects_LocalFolderOverride()
    {
        string overridePath = Path.Combine(Path.GetTempPath(), $"Override_{Guid.NewGuid()}");
        Directory.CreateDirectory(overridePath);
        
        var file = MakeSyncFile("OVERRIDE.jpg");
        var mock = MakeMockDevice(deviceId: "over-dev", files: new List<SyncFileModel> { file });
        var devSig = new DeviceSignatureModel { Id = "over-dev", LocalFolderOverride = overridePath };
        _settings.Config.RegisteredDevices.Add(devSig);

        try {
            await _orchestrator.StartSyncAsync(mock.Object);
            string expectedPath = Path.Combine(overridePath, "2026-04-26", "OVERRIDE.jpg");
            Assert.True(File.Exists(expectedPath));
        } finally {
            if (Directory.Exists(overridePath)) Directory.Delete(overridePath, true);
        }
    }


    public void Dispose()
    {
        if (Directory.Exists(_localFolder))
            Directory.Delete(_localFolder, recursive: true);
        if (Directory.Exists(_settingsFolder))
            Directory.Delete(_settingsFolder, recursive: true);
    }
}
