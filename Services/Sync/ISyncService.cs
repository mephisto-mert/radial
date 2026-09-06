using System.Threading.Tasks;

namespace RadialLauncher.Services.Sync
{
    public interface ISyncService
    {
        bool HasPatConfigured();
        string? GetGistId();
        void SavePat(string pat, string? gistId = null);
        void ClearPat();
        Task<(bool success, string filePath)> CreateLocalBackupAsync();
        System.Collections.Generic.List<string> GetLocalBackups();
        Task<bool> RestoreFromLocalBackupAsync(string filePath);
        Task<bool> ExportToFileAsync(string filePath);
        Task<bool> ImportFromFileAsync(string filePath);
        Task<(bool success, string message, string? gistId)> PushToGistAsync();
        Task<(bool success, string message)> PullFromGistAsync(string? specificGistId = null);
    }
}
