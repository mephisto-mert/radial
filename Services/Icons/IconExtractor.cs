using System;
using System.Windows.Media;

namespace RadialLauncher.Services.Icons
{
    public class IconExtractor : IIconExtractor
    {
        private readonly FileIconService _fileIconService;
        private readonly FaviconService _faviconService;

        public IconExtractor(FileIconService fileIconService, FaviconService faviconService)
        {
            _fileIconService = fileIconService ?? throw new ArgumentNullException(nameof(fileIconService));
            _faviconService = faviconService ?? throw new ArgumentNullException(nameof(faviconService));
        }

        public ImageSource? GetIconForFile(string filePath) => _fileIconService.GetIconForFile(filePath);
        public ImageSource? GetFaviconForUrl(string url) => _faviconService.GetFaviconForUrl(url);
        public ImageSource? GetBrandIcon(string name, string target) => VectorIconFactory.GetBrandIcon(name, target);
        public ImageSource CreateMonogramIcon(string name, Color bgColor) => VectorIconFactory.CreateMonogramIcon(name, bgColor);
        public ImageSource CreateIconFromVisual(Visual visual, int width = 64, int height = 64) => VectorIconFactory.CreateIconFromVisual(visual, width, height);
    }
}
