using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.Services
{
    public class ImmichSync : ISyncDestination
    {
        private readonly string _immichUrl;
        private readonly string _apiKey;
        private readonly string _deviceId;
        private static readonly HttpClient _httpClient = new HttpClient();

        public ImmichSync(string immichUrl, string apiKey, string deviceId)
        {
            // Normalize URL
            _immichUrl = immichUrl.TrimEnd('/');
            if (!_immichUrl.EndsWith("/api"))
            {
                _immichUrl += "/api";
            }

            _apiKey = apiKey;
            _deviceId = deviceId;
        }

        public async Task<bool> UploadAsync(SyncFile file, Stream fileStream)
        {
            try
            {
                if (string.IsNullOrEmpty(_immichUrl) || string.IsNullOrEmpty(_apiKey))
                    return false;

                using (var request = new HttpRequestMessage(HttpMethod.Post, $"{_immichUrl}/assets"))
                {
                    request.Headers.Add("x-api-key", _apiKey);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var content = new MultipartFormDataContent();

                    // Generate a unique deviceAssetId
                    string deviceAssetId = $"{file.FileName}_{file.Size}";

                    content.Add(new StringContent(deviceAssetId), "deviceAssetId");
                    content.Add(new StringContent(_deviceId), "deviceId");
                    content.Add(new StringContent(file.CreationTime.ToString("o")), "fileCreatedAt");
                    content.Add(new StringContent(file.CreationTime.ToString("o")), "fileModifiedAt");
                    content.Add(new StringContent("false"), "isFavorite");

                    fileStream.Position = 0;
                    var streamContent = new StreamContent(fileStream);
                    streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream"); // Or figure out from extension
                    content.Add(streamContent, "assetData", file.FileName);

                    request.Content = content;

                    var response = await _httpClient.SendAsync(request);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                    
                    // Duplicate asset might return 409 Conflict, which we can consider a "success" for syncing purposes
                    if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                    {
                        return true;
                    }

                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
