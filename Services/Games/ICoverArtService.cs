using System.Threading.Tasks;

namespace RadialLauncher.Services.Games
{
    public record CoverDownloadResult(bool Success, string Message, int DownloadedCount = 0);

    public interface ICoverArtService
    {
        string CoversDirectory { get; }
        bool HasApiKey();
        bool HasCover(int itemId);
        string? GetCoverPath(int itemId);
        Task<CoverDownloadResult> DownloadCoverAsync(int itemId, string gameName);
    }
}
