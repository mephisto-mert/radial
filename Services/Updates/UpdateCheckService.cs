using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace RadialLauncher.Services.Updates
{
    public class UpdateCheckService : IUpdateCheckService
    {
        private const string RepoOwner = "mephisto-mert";
        private const string RepoName = "radial";
        private const string ReleasesApiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

        private readonly IHttpClientFactory _httpClientFactory;

        public UpdateCheckService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(2, 0, 0);
                var client = _httpClientFactory.CreateClient("GitHubClient");

                Log.Information("Checking for updates from {Url}...", ReleasesApiUrl);
                var response = await client.GetAsync(ReleasesApiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    Log.Warning("Update check received non-success status code: {StatusCode}", response.StatusCode);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
                string body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";
                string htmlUrl = root.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? "" : "";

                string cleanTag = tagName.TrimStart('v', 'V');
                if (Version.TryParse(cleanTag, out var latestVersion))
                {
                    bool isNewer = latestVersion > currentVersion;
                    Log.Information("Update check result: Current={Current}, Latest={Latest}, Available={IsNewer}", currentVersion, latestVersion, isNewer);

                    return new UpdateInfo
                    {
                        IsUpdateAvailable = isNewer,
                        LatestVersion = cleanTag,
                        CurrentVersion = currentVersion.ToString(),
                        ReleaseNotes = body,
                        ReleaseUrl = htmlUrl
                    };
                }

                Log.Warning("Could not parse release tag version '{TagName}'", tagName);
                return null;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to check for application updates");
                return null;
            }
        }
    }
}