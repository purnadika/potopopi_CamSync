using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace PotopopiCamSync.Services
{
    public class AIDependencyManagerService
    {
        private readonly ILogger<AIDependencyManagerService> _logger;
        private readonly HttpClient _httpClient;
        private const string RepoOwner = "purnadika";
        private const string RepoName = "potopopi_CamSync";
        private const string ExpectedDll = "OpenCvSharpExtern.dll";

        public AIDependencyManagerService(ILogger<AIDependencyManagerService> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PotopopiCamSync", "1.0"));
        }

        public bool IsInstalled()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            if (File.Exists(Path.Combine(appDir, ExpectedDll))) return true;

            // Check common runtimes path for .NET Desktop apps
            string runtimesPath = Path.Combine(appDir, "runtimes", "win-x64", "native", ExpectedDll);
            if (File.Exists(runtimesPath)) return true;

            // Also check current directory if it differs from BaseDirectory
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), ExpectedDll))) return true;

            return false;
        }

        public async Task DownloadAndInstallAsync(IProgress<double> progress, CancellationToken ct)
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string tempZipFile = Path.Combine(appDir, "AI_Dependencies_temp.zip");

            try
            {
                progress.Report(5); // Finding release

                // 1. Get latest release from GitHub API
                string apiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
                _logger.LogInformation($"Fetching latest release from {apiUrl}");
                
                var response = await _httpClient.GetAsync(apiUrl, ct);
                response.EnsureSuccessStatusCode();
                
                string json = await response.Content.ReadAsStringAsync(ct);
                var releaseInfo = JObject.Parse(json);
                
                var assets = releaseInfo["assets"] as JArray;
                if (assets == null) throw new Exception("No assets found in the latest release.");

                var aiAsset = assets.FirstOrDefault(a => a["name"]?.ToString() == "AI_Dependencies.zip");
                if (aiAsset == null) throw new Exception("AI_Dependencies.zip not found in the latest release.");

                string downloadUrl = aiAsset["browser_download_url"]?.ToString() ?? throw new Exception("Download URL is empty.");

                // 2. Download the file
                _logger.LogInformation($"Downloading AI dependencies from {downloadUrl}");
                using (var downloadResponse = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    downloadResponse.EnsureSuccessStatusCode();
                    long? totalBytes = downloadResponse.Content.Headers.ContentLength;

                    using (var contentStream = await downloadResponse.Content.ReadAsStreamAsync(ct))
                    using (var fileStream = new FileStream(tempZipFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                    {
                        var buffer = new byte[81920];
                        long totalRead = 0;
                        int read;
                        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, read, ct);
                            totalRead += read;
                            if (totalBytes.HasValue)
                            {
                                double percentage = Math.Round((double)totalRead / totalBytes.Value * 80.0) + 10; // 10% to 90%
                                progress.Report(percentage);
                            }
                        }
                    }
                }

                // 3. Extract the file
                progress.Report(95);
                _logger.LogInformation("Extracting AI dependencies...");
                await Task.Run(() => ZipFile.ExtractToDirectory(tempZipFile, appDir, overwriteFiles: true), ct);

                progress.Report(100);
                _logger.LogInformation("AI dependencies installed successfully.");
            }
            finally
            {
                if (File.Exists(tempZipFile))
                {
                    try { File.Delete(tempZipFile); } catch { /* Ignore */ }
                }
            }
        }
    }
}
