using System.Windows.Media;
using RadialLauncher.Services.Icons;

namespace RadialLauncher.Services
{
    public static class IconExtractor
    {
        private static readonly FileIconService _fileIconService = new();
        private static readonly FaviconService _faviconService = new();

        public static ImageSource? GetIconForFile(string filePath) => _fileIconService.GetIconForFile(filePath);
        public static ImageSource? GetFaviconForUrl(string url) => _faviconService.GetFaviconForUrl(url);
        public static ImageSource? GetBrandIcon(string name, string target) => VectorIconFactory.GetBrandIcon(name, target);
        public static ImageSource CreateMonogramIcon(string name, Color bgColor) => VectorIconFactory.CreateMonogramIcon(name, bgColor);
        public static ImageSource CreateIconFromVisual(Visual visual, int width = 64, int height = 64) => VectorIconFactory.CreateIconFromVisual(visual, width, height);
    }
}
