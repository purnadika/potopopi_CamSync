using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PotopopiCamSync.Models;
using PotopopiCamSync.Services;

namespace PotopopiCamSync.Tests;

/// <summary>
/// Tests for ImmichSync.
/// Uses a fake HttpMessageHandler to avoid real network calls.
/// </summary>
public class ImmichSyncTests : IDisposable
{
    private readonly string _tempFile;
    private readonly SyncFile _syncFile;

    public ImmichSyncTests()
    {
        _tempFile = Path.GetTempFileName() + ".jpg";
        File.WriteAllText(_tempFile, "FAKE JPEG DATA");

        _syncFile = new SyncFile
        {
            FileName = "IMG_001.jpg",
            OriginalPath = _tempFile,
            RelativePath = "IMG_001.jpg",
            Size = new FileInfo(_tempFile).Length,
            CreationTime = new DateTime(2026, 4, 26, 10, 0, 0)
        };
    }

    private ImmichSync MakeSyncWithHandler(HttpStatusCode statusCode)
    {
        var handler = new FakeHttpHandler(statusCode);
        // We must inject via an internal helper — but ImmichSync uses a static HttpClient.
        // We'll test the logic by creating a subclass-friendly version using the public constructor
        // and verifying behavior. For full HttpClient mocking we use a custom handler factory.
        return new ImmichSync("http://immich.local", "api-key-123", "dev-001",
            NullLogger<ImmichSync>.Instance, handler);
    }

    [Fact]
    public async Task UploadAsync_Returns_True_On_Success()
    {
        var sync = MakeSyncWithHandler(HttpStatusCode.Created);
        bool result = await sync.UploadAsync(_syncFile, _tempFile);
        Assert.True(result);
    }

    [Fact]
    public async Task UploadAsync_Returns_True_On_Conflict_409()
    {
        // Immich returns 409 if the asset already exists — this counts as success
        var sync = MakeSyncWithHandler(HttpStatusCode.Conflict);
        bool result = await sync.UploadAsync(_syncFile, _tempFile);
        Assert.True(result);
    }

    [Fact]
    public async Task UploadAsync_Returns_False_On_Server_Error()
    {
        var sync = MakeSyncWithHandler(HttpStatusCode.InternalServerError);
        bool result = await sync.UploadAsync(_syncFile, _tempFile);
        Assert.False(result);
    }

    [Fact]
    public async Task UploadAsync_Returns_False_When_LocalFile_Missing()
    {
        var sync = MakeSyncWithHandler(HttpStatusCode.Created);
        bool result = await sync.UploadAsync(_syncFile, "/nonexistent/path/IMG_001.jpg");
        Assert.False(result);
    }

    [Fact]
    public async Task UploadAsync_Returns_False_When_Immich_Not_Configured()
    {
        var sync = new ImmichSync("", "", "dev-001", NullLogger<ImmichSync>.Instance);
        bool result = await sync.UploadAsync(_syncFile, _tempFile);
        Assert.False(result);
    }

    [Fact]
    public async Task UploadAsync_Respects_CancellationToken()
    {
        var sync = MakeSyncWithHandler(HttpStatusCode.Created);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Should not throw, just return false (cancellation caught internally)
        bool result = await sync.UploadAsync(_syncFile, _tempFile, cts.Token);
        Assert.False(result);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    /// <summary>Fake HttpMessageHandler for testing ImmichSync without network.</summary>
    private class FakeHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        public FakeHttpHandler(HttpStatusCode statusCode) => _statusCode = statusCode;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(_statusCode));
        }
    }
}
