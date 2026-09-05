using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Serilog;

namespace RadialLauncher.Services.Icons
{
    public class FaviconService
    {
        private readonly IHttpClientFactory? _httpClientFactory;
        private static readonly string CacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RadialLauncher", "FaviconCache");

        public FaviconService(IHttpClientFactory? httpClientFactory = null)
        {
            _httpClientFactory = httpClientFactory;
            if (!Directory.Exists(CacheDir))
            {
                Directory.CreateDirectory(CacheDir);
            }
        }

        private HttpClient CreateClient()
        {
            if (_httpClientFactory != null)
            {
                return _httpClientFactory.CreateClient("FaviconClient");
            }
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            client.Timeout = TimeSpan.FromSeconds(5);
            return client;
        }

        public ImageSource? GetFaviconForUrl(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url)) return null;

                string cleanUrl = url.Trim();
                if (!cleanUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                    !cleanUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    cleanUrl = "https://" + cleanUrl;
                }

                if (!Uri.TryCreate(cleanUrl, UriKind.Absolute, out var uri))
                    return null;

                string host = uri.Host.ToLowerInvariant();
                string safeHost = Regex.Replace(host, @"[^a-zA-Z0-9_\-\.]", "_");
                string cachedFile = Path.Combine(CacheDir, safeHost + ".png");

                if (File.Exists(cachedFile) && new FileInfo(cachedFile).Length > 0)
                {
                    var cached = LoadBitmap(cachedFile);
                    if (cached != null) return cached;
                }

                // Attempt download in background or sync
                DownloadFavicon(uri, cachedFile);

                if (File.Exists(cachedFile) && new FileInfo(cachedFile).Length > 0)
                {
                    return LoadBitmap(cachedFile);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to get favicon for URL: {Url}", url);
            }

            return null;
        }

        private void DownloadFavicon(Uri uri, string targetPath)
        {
            try
            {
                using var client = CreateClient();
                // 1. Google Favicon Service (Crisp 64px)
                string googleUrl = $"https://www.google.com/s2/favicons?domain={uri.Host}&sz=64";
                var response = client.GetAsync(googleUrl).GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode)
                {
                    var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    if (bytes != null && bytes.Length > 100) // Not generic 1x1 blank
                    {
                        File.WriteAllBytes(targetPath, bytes);
                        return;
                    }
                }

                // 2. Direct /favicon.ico fallback
                string directUrl = $"{uri.Scheme}://{uri.Host}/favicon.ico";
                var directResp = client.GetAsync(directUrl).GetAwaiter().GetResult();
                if (directResp.IsSuccessStatusCode)
                {
                    var directBytes = directResp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    if (directBytes != null && directBytes.Length > 100)
                    {
                        File.WriteAllBytes(targetPath, directBytes);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("Favicon download failed for {Host}: {Message}", uri.Host, ex.Message);
            }
        }

        private BitmapImage? LoadBitmap(string path)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Corrupt cached favicon at {Path}", path);
                try { File.Delete(path); } catch { }
                return null;
            }
        }
    }
}
