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
        private const string InstallationMarkerFile = ".ai_installed";

        public AIDependencyManagerService(ILogger<AIDependencyManagerService> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PotopopiCamSync", "1.0"));
        }

        public bool IsInstalled()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;

            // Check for marker file first (most reliable)
            string markerFile = Path.Combine(appDir, InstallationMarkerFile);
            if (File.Exists(markerFile))
            {
                _logger.LogDebug("AI modules marked as installed (marker file found).");
                return true;
            }

            // Fallback: Check for actual DLL files
            string dllPath = Path.Combine(appDir, ExpectedDll);
            if (File.Exists(dllPath))
            {
                _logger.LogDebug("AI modules detected (DLL file found). Creating marker file.");
                try
                {
                    File.WriteAllText(markerFile, "installed");
                }
                catch { /* Ignore marker file creation failure */ }
                return true;
            }

            return false;
        }

        public async Task DownloadAndInstallAsync(IProgress<double> progress, CancellationToken ct)
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string tempZipFile = Path.Combine(appDir, "AI_Dependencies_temp.zip");
            string markerFile = Path.Combine(appDir, InstallationMarkerFile);

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

                // 4. Create marker file to indicate successful installation
                try
                {
                    File.WriteAllText(markerFile, "installed");
                    _logger.LogInformation("Installation marker file created.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create installation marker file, but extraction succeeded.");
                }

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
