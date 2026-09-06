using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RadialLauncher.Services.Data;
using RadialLauncher.Services.Themes;
using Serilog;

namespace RadialLauncher.Services.Games
{
    public class CoverArtService : ICoverArtService
    {
        private readonly IThemeService _themeService;
        private readonly HttpClient _http;
        private readonly Dictionary<string, long> _searchCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _gate = new(1, 1);

        public string CoversDirectory => Path.Combine(UserDataPathProvider.Instance.GetAppDataFolder(), "covers");

        public CoverArtService(IThemeService themeService, HttpClient? httpClient = null)
        {
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
            if (!_http.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _http.DefaultRequestHeaders.UserAgent.ParseAdd("RadialLauncher/1.0");
            }
        }

        private string Key => _themeService.GetSteamGridDbKey();

        public bool HasApiKey() => !string.IsNullOrWhiteSpace(Key);

        public bool HasCover(int itemId)
        {
            string path = Path.Combine(CoversDirectory, $"{itemId}.png");
            return File.Exists(path);
        }

        public string? GetCoverPath(int itemId)
        {
            string path = Path.Combine(CoversDirectory, $"{itemId}.png");
            return File.Exists(path) ? path : null;
        }

        public async Task<CoverDownloadResult> DownloadCoverAsync(int itemId, string gameName)
        {
            if (!HasApiKey())
                return new CoverDownloadResult(false, "SteamGridDB API key is not configured.");

            string name = NormalizeGameName(gameName);
            if (string.IsNullOrEmpty(name))
                return new CoverDownloadResult(false, $"Game name is empty: {gameName}");

            Directory.CreateDirectory(CoversDirectory);
            string cachePath = Path.Combine(CoversDirectory, $"{itemId}.png");
            if (File.Exists(cachePath))
                return new CoverDownloadResult(true, "Already cached.", 0);

            await _gate.WaitAsync();
            try
            {
                // 1. Search game ID
                long gameId;
                if (_searchCache.TryGetValue(name, out long cached))
                {
                    gameId = cached;
                }
                else
                {
                    string searchUrl = $"https://www.steamgriddb.com/api/v2/search/autocomplete/{Uri.EscapeDataString(name)}";
                    using var req = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Key);
                    using var resp = await _http.SendAsync(req);

                    if ((int)resp.StatusCode == 401)
                        return new CoverDownloadResult(false, "API key rejected (401 Unauthorized).");
                    if ((int)resp.StatusCode == 429)
                        return new CoverDownloadResult(false, "Rate limit reached (429). Please wait a moment.");
                    if (!resp.IsSuccessStatusCode)
                        return new CoverDownloadResult(false, $"Search failed ({(int)resp.StatusCode}).");

                    using var doc = JsonDocument.Parse(await resp.Content.ReadAsStreamAsync());
                    if (!doc.RootElement.TryGetProperty("data", out var data) ||
                        data.ValueKind != JsonValueKind.Array ||
                        data.GetArrayLength() == 0)
                    {
                        return new CoverDownloadResult(false, $"No results found for '{gameName}'.");
                    }

                    gameId = data[0].GetProperty("id").GetInt64();
                    _searchCache[name] = gameId;
                }

                // 2. Fetch 600x900 grid posters
                string gridUrl = $"https://www.steamgriddb.com/api/v2/grids/game/{gameId}?dimensions=600x900";
                using var greq = new HttpRequestMessage(HttpMethod.Get, gridUrl);
                greq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Key);
                using var gresp = await _http.SendAsync(greq);

                if ((int)gresp.StatusCode == 429)
                    return new CoverDownloadResult(false, $"Rate limit (429) — '{gameName}' skipped.");
                if ((int)gresp.StatusCode == 404)
                    return new CoverDownloadResult(false, $"No covers found for '{gameName}'.");
                if (!gresp.IsSuccessStatusCode)
                    return new CoverDownloadResult(false, $"Cover query failed ({(int)gresp.StatusCode}).");

                using var gdoc = JsonDocument.Parse(await gresp.Content.ReadAsStreamAsync());
                string? url = null;
                if (gdoc.RootElement.TryGetProperty("data", out var gdata) && gdata.ValueKind == JsonValueKind.Array)
                {
                    foreach (var g in gdata.EnumerateArray())
                    {
                        if (g.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String)
                        {
                            string s = u.GetString() ?? string.Empty;
                            if (s.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                s.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                s.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                            {
                                url = s;
                                break;
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(url))
                    return new CoverDownloadResult(false, $"No cover URL found for '{gameName}'.");

                // 3. Download and cache
                byte[] bytes = await _http.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(cachePath, bytes);
                return new CoverDownloadResult(true, $"Cover for '{gameName}' downloaded.", 1);
            }
            catch (HttpRequestException ex)
            {
                return new CoverDownloadResult(false, $"{gameName}: network error — {ex.Message}");
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Cover download failed for {Name}", gameName);
                return new CoverDownloadResult(false, $"{gameName}: {ex.Message}");
            }
            finally
            {
                _gate.Release();
            }
        }

        public static string NormalizeGameName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            string n = raw
                .Replace("™", string.Empty)
                .Replace("®", string.Empty)
                .Trim();

            int dash = n.IndexOf(" - ", StringComparison.Ordinal);
            if (dash > 0) n = n.Substring(0, dash).Trim();

            n = n
                .Replace(" (PC)", string.Empty)
                .Replace(" (Demo)", string.Empty)
                .Replace(" (Beta)", string.Empty)
                .Trim();
            return n;
        }
    }
}
