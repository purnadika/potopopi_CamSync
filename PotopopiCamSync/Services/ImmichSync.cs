using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
            _httpClient = handler != null ? new HttpClient(handler) : new HttpClient();
        }

        /// <summary>
        /// Streams the file at localFilePath directly to the Immich API.
        /// No full-file buffering in memory.
        /// </summary>
        public async Task<bool> UploadAsync(SyncFile file, string localFilePath, CancellationToken cancellationToken = default)
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

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{_immichUrl}/assets");
                request.Headers.Add("x-api-key", _apiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var content = new MultipartFormDataContent();
                string deviceAssetId = $"{file.FileName}_{file.Size}";

                content.Add(new StringContent(deviceAssetId), "deviceAssetId");
                content.Add(new StringContent(_deviceId), "deviceId");
                content.Add(new StringContent(file.CreationTime.ToString("o")), "fileCreatedAt");
                content.Add(new StringContent(file.CreationTime.ToString("o")), "fileModifiedAt");
                content.Add(new StringContent("false"), "isFavorite");

                // Stream directly from disk — no MemoryStream
                var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
                var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(GetMimeType(file.FileName));
                content.Add(streamContent, "assetData", file.FileName);

                request.Content = content;

                var response = await _httpClient.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict)
                {
                    _logger.LogInformation("Immich upload success: {File}", file.FileName);
                    return true;
                }

                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Immich upload failed for {File}: {Status} — {Body}", file.FileName, response.StatusCode, body);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Immich upload error for {File}", file.FileName);
                return false;
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
