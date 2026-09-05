using System.Threading.Tasks;

namespace RadialLauncher.Services.Sync
{
    public interface ISyncService
    {
        Task<bool> ExportToFileAsync(string filePath);
        Task<bool> ImportFromFileAsync(string filePath);
        Task<bool> SyncToLocalNetworkFolderAsync(string sharedFolderPath);
        Task<bool> SyncFromLocalNetworkFolderAsync(string sharedFolderPath);
    }
}
