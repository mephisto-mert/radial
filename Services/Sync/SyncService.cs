using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using RadialLauncher.Data.Repositories;
using RadialLauncher.Models;
using RadialLauncher.Services.Localization;
using RadialLauncher.Services.Themes;
using Serilog;

namespace RadialLauncher.Services.Sync
{
    public class SyncService : ISyncService
    {
        private static readonly byte[] Entropy = new byte[] { 0x52, 0x61, 0x64, 0x69, 0x61, 0x6C, 0x53, 0x79, 0x6E, 0x63 }; // "RadialSync"
        private static readonly string VaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RadialLauncher", "sync_vault.bin");

        private readonly IItemRepository _itemRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly IHttpClientFactory _httpClientFactory;

        public class SyncPayload
        {
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;
            public string AppVersion { get; set; } = "1.0";
            public ThemeService.AppSettings? Settings { get; set; }
            public List<Category> Categories { get; set; } = new();
            public List<LauncherItem> Items { get; set; } = new();
        }

        private class EncryptedVaultData
        {
            public string Pat { get; set; } = string.Empty;
            public string? GistId { get; set; }
        }

        public SyncService(IItemRepository itemRepo, ICategoryRepository categoryRepo, IHttpClientFactory httpClientFactory)
        {
            _itemRepo = itemRepo;
            _categoryRepo = categoryRepo;
            _httpClientFactory = httpClientFactory;
        }

        public bool HasPatConfigured()
        {
            var vault = LoadVault();
            return !string.IsNullOrWhiteSpace(vault?.Pat);
        }

        public string? GetGistId()
        {
            var vault = LoadVault();
            return vault?.GistId;
        }

        public void SavePat(string pat, string? gistId = null)
        {
            try
            {
                var vault = LoadVault() ?? new EncryptedVaultData();
                vault.Pat = pat.Trim();
                if (!string.IsNullOrWhiteSpace(gistId))
                {
                    vault.GistId = gistId.Trim();
                }

                SaveVault(vault);
                Log.Information("GitHub Personal Access Token saved securely.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed saving encrypted PAT to vault");
                throw;
            }
        }

        public void ClearPat()
        {
            try
            {
                if (File.Exists(VaultPath))
                {
                    File.Delete(VaultPath);
                    Log.Information("Cleared sync vault credentials.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed clearing sync vault credentials");
            }
        }

        private EncryptedVaultData? LoadVault()
        {
            try
            {
                if (!File.Exists(VaultPath)) return null;
                byte[] cipherBytes = File.ReadAllBytes(VaultPath);
                byte[] plainBytes = ProtectedData.Unprotect(cipherBytes, Entropy, DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(plainBytes);
                return JsonSerializer.Deserialize<EncryptedVaultData>(json);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not decrypt or read sync vault credentials");
                return null;
            }
        }

        private void SaveVault(EncryptedVaultData data)
        {
            string dir = Path.GetDirectoryName(VaultPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(data);
            byte[] plainBytes = Encoding.UTF8.GetBytes(json);
            byte[] cipherBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(VaultPath, cipherBytes);
        }

        private static readonly string BackupsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RadialLauncher", "Backups");

        public async Task<(bool success, string filePath)> CreateLocalBackupAsync()
        {
            try
            {
                if (!Directory.Exists(BackupsDir)) Directory.CreateDirectory(BackupsDir);

                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                string fileName = $"backup-{timestamp}.json";
                string targetPath = Path.Combine(BackupsDir, fileName);
                string tmpPath = $"{targetPath}.tmp";

                var payload = new SyncPayload
                {
                    Settings = ThemeService.Instance.GetSettings(),
                    Categories = _categoryRepo.GetAll(),
                    Items = _itemRepo.GetAll()
                };

                string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(tmpPath, json);
                File.Move(tmpPath, targetPath, overwrite: true);

                // Rotate backups: keep last 10
                RotateBackups(10);

                Log.Information("Local backup created at {Path}", targetPath);
                return (true, targetPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create local backup");
                return (false, string.Empty);
            }
        }

        public List<string> GetLocalBackups()
        {
            try
            {
                if (!Directory.Exists(BackupsDir)) return new List<string>();
                return Directory.GetFiles(BackupsDir, "backup-*.json")
                                .OrderByDescending(f => File.GetCreationTimeUtc(f))
                                .ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to list local backups");
                return new List<string>();
            }
        }

        public async Task<bool> RestoreFromLocalBackupAsync(string filePath)
        {
            return await ImportFromFileAsync(filePath);
        }

        private void RotateBackups(int keepCount)
        {
            try
            {
                if (!Directory.Exists(BackupsDir)) return;
                var files = Directory.GetFiles(BackupsDir, "backup-*.json")
                                     .Select(f => new FileInfo(f))
                                     .OrderByDescending(f => f.CreationTimeUtc)
                                     .ToList();

                if (files.Count > keepCount)
                {
                    for (int i = keepCount; i < files.Count; i++)
                    {
                        try
                        {
                            files[i].Delete();
                            Log.Debug("Rotated old backup file {Path}", files[i].FullName);
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, "Could not delete old backup {Path}", files[i].FullName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to rotate backup files");
            }
        }

        public async Task<bool> ExportToFileAsync(string filePath)
        {
            try
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string tmpPath = $"{filePath}.tmp";
                var payload = new SyncPayload
                {
                    Settings = ThemeService.Instance.GetSettings(),
                    Categories = _categoryRepo.GetAll(),
                    Items = _itemRepo.GetAll()
                };
                string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(tmpPath, json);
                File.Move(tmpPath, filePath, overwrite: true);

                Log.Information("Exported launcher data to {Path}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to export launcher data");
                return false;
            }
        }

        public async Task<bool> ImportFromFileAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return false;
                string json = await File.ReadAllTextAsync(filePath);
                return ApplyPayloadJson(json);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to import launcher data from {Path}", filePath);
                return false;
            }
        }

        private bool ApplyPayloadJson(string json)
        {
            try
            {
                var payload = JsonSerializer.Deserialize<SyncPayload>(json);
                if (payload == null) return false;

                if (payload.Settings != null)
                {
                    ThemeService.Instance.UpdateSettings(payload.Settings);
                    if (!string.IsNullOrEmpty(payload.Settings.Language))
                    {
                        LocalizationService.Instance.SetLanguage(payload.Settings.Language);
                    }
                }

                foreach (var cat in payload.Categories)
                {
                    var existing = _categoryRepo.GetById(cat.Id);
                    if (existing == null) _categoryRepo.Insert(cat);
                    else _categoryRepo.Update(cat);
                }
                foreach (var item in payload.Items)
                {
                    var existing = _itemRepo.GetById(item.Id);
                    if (existing == null) _itemRepo.Insert(item);
                    else _itemRepo.Update(item);
                }
                Log.Information("Successfully imported {ItemCount} items, {CatCount} categories",
                    payload.Items.Count, payload.Categories.Count);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error parsing and applying sync payload JSON");
                return false;
            }
        }

        public async Task<(bool success, string message, string? gistId)> PushToGistAsync()
        {
            var vault = LoadVault();
            if (vault == null || string.IsNullOrWhiteSpace(vault.Pat))
            {
                return (false, "GitHub Personal Access Token (PAT) ayarlanmamış.", null);
            }

            try
            {
                var payload = new SyncPayload
                {
                    Settings = ThemeService.Instance.GetSettings(),
                    Categories = _categoryRepo.GetAll(),
                    Items = _itemRepo.GetAll()
                };
                string backupJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });

                var gistRequestBody = new
                {
                    description = "Radial Launcher Settings & Items Backup",
                    @public = false,
                    files = new Dictionary<string, object>
                    {
                        { "radial_backup.json", new { content = backupJson } }
                    }
                };

                using var client = _httpClientFactory.CreateClient("GitHubClient");
                client.DefaultRequestHeaders.UserAgent.Clear();
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RadialLauncher", "2.0"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", vault.Pat);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

                string? existingGistId = vault.GistId;
                HttpResponseMessage response;

                if (!string.IsNullOrWhiteSpace(existingGistId))
                {
                    // Attempt PATCH to update existing Gist
                    var patchContent = new StringContent(JsonSerializer.Serialize(gistRequestBody), Encoding.UTF8, "application/json");
                    var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"https://api.github.com/gists/{existingGistId}")
                    {
                        Content = patchContent
                    };
                    response = await client.SendAsync(patchRequest);
                    if (!response.IsSuccessStatusCode && response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // Existing gist not found; create a new one
                        var postContent = new StringContent(JsonSerializer.Serialize(gistRequestBody), Encoding.UTF8, "application/json");
                        response = await client.PostAsync("https://api.github.com/gists", postContent);
                    }
                }
                else
                {
                    var postContent = new StringContent(JsonSerializer.Serialize(gistRequestBody), Encoding.UTF8, "application/json");
                    response = await client.PostAsync("https://api.github.com/gists", postContent);
                }

                if (!response.IsSuccessStatusCode)
                {
                    string errContent = await response.Content.ReadAsStringAsync();
                    Log.Error("GitHub Gist push failed with HTTP {Status}: {Body}", response.StatusCode, errContent);
                    return (false, $"GitHub Hatası: {response.StatusCode}", null);
                }

                string respJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(respJson);
                string newGistId = doc.RootElement.GetProperty("id").GetString() ?? "";

                vault.GistId = newGistId;
                SaveVault(vault);

                Log.Information("Settings successfully pushed to GitHub Gist: {Id}", newGistId);
                return (true, "Yedek GitHub Gist'e başarıyla yüklendi!", newGistId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception pushing settings to GitHub Gist");
                return (false, $"Hata: {ex.Message}", null);
            }
        }

        public async Task<(bool success, string message)> PullFromGistAsync(string? specificGistId = null)
        {
            var vault = LoadVault();
            if (vault == null || string.IsNullOrWhiteSpace(vault.Pat))
            {
                return (false, "GitHub Personal Access Token (PAT) ayarlanmamış.");
            }

            try
            {
                using var client = _httpClientFactory.CreateClient("GitHubClient");
                client.DefaultRequestHeaders.UserAgent.Clear();
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RadialLauncher", "2.0"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", vault.Pat);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

                string? gistId = specificGistId ?? vault.GistId;

                if (string.IsNullOrWhiteSpace(gistId))
                {
                    // Find gist with radial_backup.json from user's gists list
                    var listResp = await client.GetAsync("https://api.github.com/gists");
                    if (!listResp.IsSuccessStatusCode)
                    {
                        return (false, $"Gist listesi alınamadı: {listResp.StatusCode}");
                    }
                    string listJson = await listResp.Content.ReadAsStringAsync();
                    using var listDoc = JsonDocument.Parse(listJson);
                    foreach (var elem in listDoc.RootElement.EnumerateArray())
                    {
                        if (elem.TryGetProperty("files", out var filesElem) && filesElem.TryGetProperty("radial_backup.json", out _))
                        {
                            gistId = elem.GetProperty("id").GetString();
                            break;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(gistId))
                {
                    return (false, "Hesabınızda 'radial_backup.json' içeren bir Gist bulunamadı.");
                }

                var getResp = await client.GetAsync($"https://api.github.com/gists/{gistId}");
                if (!getResp.IsSuccessStatusCode)
                {
                    return (false, $"Gist indirilemedi: {getResp.StatusCode}");
                }

                string gistJson = await getResp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(gistJson);
                var files = doc.RootElement.GetProperty("files");
                if (!files.TryGetProperty("radial_backup.json", out var fileProp))
                {
                    return (false, "Gist içerisinde 'radial_backup.json' dosyası bulunamadı.");
                }

                string content;
                if (fileProp.TryGetProperty("content", out var contentProp) && !string.IsNullOrEmpty(contentProp.GetString()))
                {
                    content = contentProp.GetString()!;
                }
                else if (fileProp.TryGetProperty("raw_url", out var rawUrlProp))
                {
                    content = await client.GetStringAsync(rawUrlProp.GetString()!);
                }
                else
                {
                    return (false, "Gist içeriği boş veya okunamadı.");
                }

                bool applied = ApplyPayloadJson(content);
                if (applied)
                {
                    vault.GistId = gistId;
                    SaveVault(vault);
                    return (true, "Ayarlar ve öğeler GitHub Gist'ten başarıyla yüklendi!");
                }
                return (false, "Gist dosya içeriği ayrıştırılamadı.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception pulling settings from GitHub Gist");
                return (false, $"Hata: {ex.Message}");
            }
        }
    }
}
