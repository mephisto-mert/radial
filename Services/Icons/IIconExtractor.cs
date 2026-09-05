using System.Windows.Media;

namespace RadialLauncher.Services.Icons
{
    public interface IIconExtractor
    {
        ImageSource? GetIconForFile(string filePath);
        ImageSource? GetFaviconForUrl(string url);
        ImageSource? GetBrandIcon(string name, string target);
        ImageSource CreateMonogramIcon(string name, Color bgColor);
        ImageSource CreateIconFromVisual(Visual visual, int width = 64, int height = 64);
    }
}
