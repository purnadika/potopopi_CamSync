using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Reflection;
using System.Net.Http;

namespace PotopopiCamSync.Services;

/// <summary>
/// Checks GitHub releases for app updates.
/// </summary>
public class UpdateChecker
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdateChecker> _logger;
    private const string GitHubApiUrl = "https://api.github.com/repos/purnadika/potopopi_CamSync/releases/latest";

    public UpdateChecker(HttpClient httpClient, ILogger<UpdateChecker> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets current app version from assembly.
    /// </summary>
    public string GetCurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return $"v{version?.Major}.{version?.Minor}.{version?.Build}";
    }

    /// <summary>
    /// Checks GitHub for latest release. Returns null if up-to-date or error.
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5)); // 5s timeout

            var request = new HttpRequestMessage(HttpMethod.Get, GitHubApiUrl);
            request.Headers.Add("User-Agent", "PotopopiCamSync");

            using var response = await _httpClient.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"GitHub API returned {response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cts.Token);
            var release = JsonConvert.DeserializeObject<GitHubRelease>(json);

            if (release?.TagName == null)
            {
                _logger.LogWarning("Invalid GitHub response: no tag_name");
                return null;
            }

            var current = GetCurrentVersion();
            if (IsNewerVersion(release.TagName, current))
            {
                return new UpdateInfo
                {
                    CurrentVersion = current,
                    LatestVersion = release.TagName,
                    ReleaseUrl = release.HtmlUrl,
                    ReleaseNotes = release.Body
                };
            }

            _logger.LogInformation($"App is up-to-date: {current}");
            return null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Update check timed out");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Update check failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Compares semantic versions (e.g., "v1.2.3" vs "v1.2.4").
    /// </summary>
    private bool IsNewerVersion(string latestTag, string currentTag)
    {
        var latest = TrimVersionTag(latestTag);
        var current = TrimVersionTag(currentTag);

        var latestParts = latest.Split('.').Select(p => int.TryParse(p, out var i) ? i : 0).ToArray();
        var currentParts = current.Split('.').Select(p => int.TryParse(p, out var i) ? i : 0).ToArray();

        for (int i = 0; i < 3; i++)
        {
            var l = i < latestParts.Length ? latestParts[i] : 0;
            var c = i < currentParts.Length ? currentParts[i] : 0;

            if (l > c) return true;
            if (l < c) return false;
        }

        return false;
    }

    private string TrimVersionTag(string tag) => tag.StartsWith("v") ? tag.Substring(1) : tag;

    /// <summary>
    /// GitHub API response for latest release.
    /// </summary>
    private class GitHubRelease
    {
        [JsonProperty("tag_name")]
        public string? TagName { get; set; }

        [JsonProperty("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonProperty("body")]
        public string? Body { get; set; }
    }
}

/// <summary>
/// Update notification info.
/// </summary>
public record UpdateInfo
{
    public string? CurrentVersion { get; set; }
    public string? LatestVersion { get; set; }
    public string? ReleaseUrl { get; set; }
    public string? ReleaseNotes { get; set; }
}
