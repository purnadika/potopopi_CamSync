using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Polly;
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

            if (handler != null)
            {
                _httpClient = new HttpClient(handler);
            }
            else
            {
                var socketsHandler = new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                    KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                    KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                    EnableMultipleHttp2Connections = true
                };
                _httpClient = new HttpClient(socketsHandler);
            }
            
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "PotopopiCamSync/1.3.0-dev");
            _httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            _httpClient.DefaultRequestVersion = HttpVersion.Version11;
            _httpClient.Timeout = TimeSpan.FromMinutes(30);
            
            _retryPolicy = CreateRetryPolicy();
        }


        private IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy()
        {
            return Policy
                .Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .Or<IOException>()
                .OrResult<HttpResponseMessage>(r =>
                    r.StatusCode == HttpStatusCode.RequestTimeout ||
                    r.StatusCode == HttpStatusCode.ServiceUnavailable ||
                    r.StatusCode == HttpStatusCode.BadGateway ||
                    r.StatusCode == HttpStatusCode.GatewayTimeout)
                .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    (outcome, delay, retryCount, context) =>
                    {
                        _logger.LogWarning("Retry {RetryCount}/3 in {DelaySeconds}s for Immich upload ({Error})",
                            retryCount, delay.TotalSeconds, outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                    });
        }

        public async Task<bool> UploadAsync(SyncFile file, string localFilePath, CancellationToken cancellationToken = default)
            => await UploadAsync(file, localFilePath, null, cancellationToken);

        public async Task<bool> UploadAsync(SyncFile file, string localFilePath, string? albumName, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(_immichUrl) || string.IsNullOrEmpty(_apiKey)) return false;
                if (!File.Exists(localFilePath)) { _logger.LogWarning("Local file not found: {Path}", localFilePath); return false; }

                // Use chunked upload for files > 50MB to bypass proxy limits (Cloudflare etc.)
                if (file.Size > 50 * 1024 * 1024)
                {
                    return await UploadChunkedAsync(file, localFilePath, albumName, cancellationToken);
                }

                return await UploadSingleAsync(file, localFilePath, albumName, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Immich upload error {File}: {Message}", file.FileName, ex.Message);
                return false;
            }
        }

        private async Task<bool> UploadSingleAsync(SyncFile file, string localFilePath, string? albumName, CancellationToken cancellationToken)
        {
            var response = await _retryPolicy.ExecuteAsync(async (ct) =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{_immichUrl}/assets");
                request.Headers.Add("x-api-key", _apiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.ExpectContinue = false;

                using var content = new MultipartFormDataContent();
                content.Add(new StringContent($"{file.FileName}_{file.Size}"), "deviceAssetId");
                content.Add(new StringContent(_deviceId), "deviceId");
                content.Add(new StringContent(file.CreationTime.ToString("o")), "fileCreatedAt");
                content.Add(new StringContent(file.CreationTime.ToString("o")), "fileModifiedAt");
                content.Add(new StringContent("false"), "isFavorite");

                using var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
                using var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(GetMimeType(file.FileName));
                content.Add(streamContent, "assetData", file.FileName);
                request.Content = content;

                return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            }, cancellationToken);

            return await HandleResponseAsync(response, file, albumName, cancellationToken);
        }

        private async Task<bool> UploadChunkedAsync(SyncFile file, string localFilePath, string? albumName, CancellationToken cancellationToken)
        {
            const long chunkSize = 30 * 1024 * 1024; // 30MB per chunk
            int totalChunks = (int)Math.Ceiling((double)file.Size / chunkSize);
            string assetId = Guid.NewGuid().ToString();
            
            _logger.LogInformation("Starting chunked upload for {File} ({Size}MB, {Count} chunks)", 
                file.FileName, file.Size / (1024 * 1024), totalChunks);

            for (int i = 0; i < totalChunks; i++)
            {
                int chunkIndex = i;
                long offset = i * chunkSize;
                long bytesToRead = Math.Min(chunkSize, file.Size - offset);

                var response = await _retryPolicy.ExecuteAsync(async (ct) =>
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, $"{_immichUrl}/assets");
                    request.Headers.Add("x-api-key", _apiKey);
                    request.Headers.Add("x-immich-chunk-index", chunkIndex.ToString());
                    request.Headers.Add("x-immich-chunk-count", totalChunks.ToString());
                    request.Headers.Add("x-immich-asset-id", assetId);
                    
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    using var content = new MultipartFormDataContent();
                    content.Add(new StringContent($"{file.FileName}_{file.Size}"), "deviceAssetId");
                    content.Add(new StringContent(_deviceId), "deviceId");
                    content.Add(new StringContent(file.CreationTime.ToString("o")), "fileCreatedAt");
                    content.Add(new StringContent(file.CreationTime.ToString("o")), "fileModifiedAt");
                    content.Add(new StringContent("false"), "isFavorite");

                    // Read chunk
                    byte[] buffer = new byte[bytesToRead];
                    using (var fs = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        fs.Seek(offset, SeekOrigin.Begin);
                        await fs.ReadExactlyAsync(buffer, 0, (int)bytesToRead, ct);
                    }

                    var streamContent = new ByteArrayContent(buffer);
                    streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(GetMimeType(file.FileName));
                    content.Add(streamContent, "assetData", file.FileName);
                    request.Content = content;

                    return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                }, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return await HandleResponseAsync(response, file, albumName, cancellationToken);
                }
                
                _logger.LogDebug("Chunk {Index}/{Total} uploaded for {File}", chunkIndex + 1, totalChunks, file.FileName);
            }

            _logger.LogInformation("Chunked upload completed for {File}", file.FileName);
            return true;
        }

        private async Task<bool> HandleResponseAsync(HttpResponseMessage response, SyncFile file, string? albumName, CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                var assetResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(body);
                string? assetId = assetResponse?.id;

                if (!string.IsNullOrWhiteSpace(assetId) && !string.IsNullOrWhiteSpace(albumName))
                {
                    await AddAssetToAlbumAsync(assetId, albumName, cancellationToken);
                }
                return true;
            }
            else if (response.StatusCode == HttpStatusCode.Conflict)
            {
                return true;
            }

            string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge)
            {
                string hint = "";
                if (errorBody.Contains("cloudflare", StringComparison.OrdinalIgnoreCase))
                {
                    hint = "\n[CLOUDFLARE DETECTED] Cloudflare Free has a 100MB limit. " +
                           "Even chunked upload failed? Try bypassing Cloudflare (use direct IP).";
                    try { PotopopiCamSync.App.ShowTrayNotification("Upload Failed", $"Cloudflare blocked '{file.FileName}'. Use direct IP."); } catch { }
                }
                _logger.LogError("Immich upload failed {File}: 413 Payload Too Large. {Hint}", file.FileName, hint);
            }
            else
            {
                _logger.LogWarning("Immich upload failed {File}: {Status} - {Body}", file.FileName, response.StatusCode, errorBody);
            }
            return false;
        }

        private async Task AddAssetToAlbumAsync(string assetId, string albumName, CancellationToken ct)
        {
            try
            {
                string? albumId = await GetOrCreateAlbumAsync(albumName, ct);
                if (string.IsNullOrEmpty(albumId)) return;

                using var request = new HttpRequestMessage(HttpMethod.Put, $"{_immichUrl}/albums/{albumId}/assets");
                request.Headers.Add("x-api-key", _apiKey);
                
                var payload = new { ids = new[] { assetId } };
                request.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(payload), System.Text.Encoding.UTF8, "application/json");

                var res = await _httpClient.SendAsync(request, ct);
                if (res.IsSuccessStatusCode) _logger.LogInformation("Assigned {AssetId} to album '{Album}'", assetId, albumName);
                else _logger.LogWarning("Failed to assign to album: {Status}", res.StatusCode);
            }
            catch (Exception ex) { _logger.LogWarning("Album assignment failed: {Msg}", ex.Message); }
        }

        private async Task<string?> GetOrCreateAlbumAsync(string albumName, CancellationToken ct)
        {
            try
            {
                // Find existing
                using var getReq = new HttpRequestMessage(HttpMethod.Get, $"{_immichUrl}/albums");
                getReq.Headers.Add("x-api-key", _apiKey);
                var getRes = await _httpClient.SendAsync(getReq, ct);
                if (getRes.IsSuccessStatusCode)
                {
                    var albumsBody = await getRes.Content.ReadAsStringAsync(ct);
                    var albums = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic[]>(albumsBody);
                    var existing = albums?.FirstOrDefault(a => (string)a.albumName == albumName);
                    if (existing != null) return (string)existing.id;
                }

                // Create new
                using var postReq = new HttpRequestMessage(HttpMethod.Post, $"{_immichUrl}/albums");
                postReq.Headers.Add("x-api-key", _apiKey);
                var payload = new { albumName = albumName };
                postReq.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(payload), System.Text.Encoding.UTF8, "application/json");
                
                var postRes = await _httpClient.SendAsync(postReq, ct);
                if (postRes.IsSuccessStatusCode)
                {
                    var resBody = await postRes.Content.ReadAsStringAsync(ct);
                    var newAlbum = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(resBody);
                    string? newId = newAlbum?.id;
                    return newId;
                }
            }
            catch (Exception ex) { _logger.LogWarning("GetOrCreateAlbum failed: {Msg}", ex.Message); }
            return null;
        }

        private static string GetMimeType(string fileName)

        {
            string ext = Path.GetExtension(fileName).ToUpperInvariant();
            return ext switch
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
