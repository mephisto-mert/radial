using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RadialLauncher.Services
{
    public static class IconExtractor
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private static string FaviconCacheDir
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RadialLauncher", "FaviconCache");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static ImageSource? GetFaviconForUrl(string urlTarget)
        {
            try
            {
                string domain = urlTarget;
                if (domain.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    domain = new Uri(domain).Host;
                }
                else
                {
                    try { domain = new Uri("https://" + domain).Host; }
                    catch { }
                }

                string safeName = domain.Replace(".", "_").Replace(":", "_");
                string cachePath = Path.Combine(FaviconCacheDir, safeName + ".png");

                if (File.Exists(cachePath))
                {
                    var fileAge = DateTime.Now - File.GetLastWriteTime(cachePath);
                    if (fileAge.TotalDays < 7)
                    {
                        return LoadImageFromFile(cachePath);
                    }
                }

                string faviconUrl = $"https://www.google.com/s2/favicons?domain={Uri.EscapeDataString(domain)}&sz=64";
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    var data = client.GetByteArrayAsync(faviconUrl).GetAwaiter().GetResult();
                    if (data != null && data.Length > 100)
                    {
                        File.WriteAllBytes(cachePath, data);
                        return LoadImageFromFile(cachePath);
                    }
                }
            }
            catch { }
            return null;
        }

        public static ImageSource? LoadImageFromFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public static ImageSource? GetIconForFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            // Direct image file (.png, .jpg, .ico)
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".ico")
            {
                var img = LoadImageFromFile(path);
                if (img != null) return img;
            }

            // Steam URL lookup
            if (path.StartsWith("steam://rungameid/", StringComparison.OrdinalIgnoreCase))
            {
                string appId = path.Substring("steam://rungameid/".Length).Trim();
                var steamIcons = GameDetector.ScanSteamShortcutIcons();
                if (steamIcons.TryGetValue(appId, out var iconFile) && File.Exists(iconFile))
                {
                    var img = LoadImageFromFile(iconFile);
                    if (img != null) return img;
                }
            }

            SHFILEINFO shinfo = new SHFILEINFO();
            IntPtr result = SHGetFileInfo(path, 0, out shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON);
            
            if (result == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero)
            {
                result = SHGetFileInfo(path, 0x80, out shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES);
            }

            if (shinfo.hIcon != IntPtr.Zero)
            {
                try
                {
                    ImageSource img = Imaging.CreateBitmapSourceFromHIcon(
                        shinfo.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    
                    img.Freeze();
                    return img;
                }
                catch
                {
                    return null;
                }
                finally
                {
                    DestroyIcon(shinfo.hIcon);
                }
            }

            return null;
        }
    }
}
