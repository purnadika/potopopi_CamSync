using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.Services
{
    public class ImmichSync : ISyncDestination
    {
        private readonly string _immichUrl;
        private readonly string _apiKey;
        private readonly string _deviceId;
        private readonly ILogger<ImmichSync> _logger;
        private readonly HttpClient _httpClient;
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

        public ImmichSync(string immichUrl, string apiKey, string deviceId, ILogger<ImmichSync> logger)
            : this(immichUrl, apiKey, deviceId, logger, null)
        {
        }

        internal ImmichSync(string immichUrl, string apiKey, string deviceId, ILogger<ImmichSync> logger, HttpMessageHandler? handler)
        {
            _immichUrl = immichUrl.TrimEnd('/');
            if (!_immichUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
                _immichUrl += "/api";

            _apiKey = apiKey;
            _deviceId = deviceId;
            _logger = logger;
            _httpClient = handler is not null ? new HttpClient(handler) : new HttpClient();
            
            // Set timeout to 30 minutes for large video uploads (GoPro etc)
            _httpClient.Timeout = TimeSpan.FromMinutes(30);
            
            _retryPolicy = CreateRetryPolicy();
        }

        private IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy()
        {
            return Policy
                .Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .Or<IOException>() // Handle "connection forcibly closed"
                .OrResult<HttpResponseMessage>(r =>
                    r.StatusCode == System.Net.HttpStatusCode.RequestTimeout ||
                    r.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                    r.StatusCode == System.Net.HttpStatusCode.BadGateway ||
                    r.StatusCode == System.Net.HttpStatusCode.GatewayTimeout)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt =>
                        TimeSpan.FromSeconds(Math.Pow(2, attempt)), // 2s, 4s, 8s
                    onRetry: (outcome, delay, retryCount, context) =>
                    {
                        var errorMessage = outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString();
                        _logger.LogWarning("Retry {RetryCount}/3 in {DelaySeconds}s for Immich upload ({Error})",
                            retryCount, delay.TotalSeconds, errorMessage);
                    });
        }

        /// <summary>
        /// Streams the file at localFilePath directly to the Immich API.
        /// No full-file buffering in memory.
        /// Includes exponential backoff retry on network failures.
        /// </summary>
        public async Task<bool> UploadAsync(SyncFile file, string localFilePath, CancellationToken cancellationToken = default)
            => await UploadAsync(file, localFilePath, albumName: null, cancellationToken);

        /// <summary>
        /// Streams the file at localFilePath directly to the Immich API with optional album assignment.
        /// </summary>
        public async Task<bool> UploadAsync(SyncFile file, string localFilePath, string? albumName, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(_immichUrl) || string.IsNullOrEmpty(_apiKey))
                    return false;

                if (!File.Exists(localFilePath))
                {
                    _logger.LogWarning("Immich upload skipped — local file not found: {Path}", localFilePath);
                    return false;
                }

                var response = await _retryPolicy.ExecuteAsync(async (ct) =>
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, $"{_immichUrl}/assets");
                    request.Headers.Add("x-api-key", _apiKey);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    using var content = new MultipartFormDataContent();
                    string deviceAssetId = $"{file.FileName}_{file.Size}";

                    content.Add(new StringContent(deviceAssetId), "deviceAssetId");
                    content.Add(new StringContent(_deviceId), "deviceId");
                    content.Add(new StringContent(file.CreationTime.ToString("o")), "fileCreatedAt");
                    content.Add(new StringContent(file.CreationTime.ToString("o")), "fileModifiedAt");
                    content.Add(new StringContent("false"), "isFavorite");

                    // Stream directly from disk — no MemoryStream
                    // Use a larger buffer for video files
                    using var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1024 * 1024, useAsync: true);
                    using var streamContent = new StreamContent(fileStream);
                    streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(GetMimeType(file.FileName));
                    content.Add(streamContent, "assetData", file.FileName);

                    request.Content = content;
                    
                    // Use completionOption: ResponseHeadersRead to avoid buffering response body
                    return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                }, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Immich upload success: {File}", file.FileName);

                    // Assign to album if specified
                    if (!string.IsNullOrWhiteSpace(albumName))
                    {
                        await TryAssignToAlbumAsync(file, albumName, cancellationToken);
                    }

                    return true;
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    _logger.LogInformation("Immich upload skipped (Already exists): {File}", file.FileName);
                    return true;
                }

                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Immich upload failed for {File}: {Status} — {Body}", file.FileName, response.StatusCode, body);
                return false;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Immich upload cancelled for {File}", file.FileName);
                return false;
            }
            catch (IOException ioEx) when (ioEx.Message.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError(ioEx, "Immich server forcibly closed the connection for {File}. This usually means the file is too large for the server's configuration (check Nginx/Immich client_max_body_size).", file.FileName);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Immich upload error for {File}. Status: {Message}", file.FileName, ex.Message);
                return false;
            }
        }

        private async Task TryAssignToAlbumAsync(SyncFile file, string albumName, CancellationToken cancellationToken)
        {
            try
            {
                // This is a best-effort operation; failures are logged but don't fail the sync
                // In a real implementation, you'd need to:
                // 1. Query albums API to get album ID by name
                // 2. Add asset to album

                _logger.LogDebug("Album assignment for {File} to '{Album}' not yet implemented", file.FileName, albumName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not assign {File} to album {Album}", file.FileName, albumName);
            }
        }

        private static string GetMimeType(string fileName)
        {
            string ext = Path.GetExtension(fileName);
            return ext.ToUpperInvariant() switch
            {
                ".JPG" or ".JPEG" => "image/jpeg",
                ".PNG"            => "image/png",
                ".CR2" or ".CR3"  => "image/x-canon-cr2",
                ".NEF"            => "image/x-nikon-nef",
                ".ARW"            => "image/x-sony-arw",
                ".DNG"            => "image/x-adobe-dng",
                ".MP4"            => "video/mp4",
                ".MOV"            => "video/quicktime",
                ".AVI"            => "video/x-msvideo",
                _                 => "application/octet-stream"
            };
        }
    }
}
