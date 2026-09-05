using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Serilog;

namespace RadialLauncher.Services.Icons
{
    public class FileIconService
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

        public ImageSource? GetIconForFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return null;

            try
            {
                // 1. If shortcut .lnk, resolve real target and icon
                if (filePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    var resolved = ResolveShortcut(filePath);
                    if (resolved != null)
                    {
                        if (!string.IsNullOrEmpty(resolved.IconLocation) && File.Exists(resolved.IconLocation))
                        {
                            var ico = LoadImageFromFile(resolved.IconLocation);
                            if (ico != null) return ico;
                        }
                        if (!string.IsNullOrEmpty(resolved.TargetPath) && File.Exists(resolved.TargetPath))
                        {
                            filePath = resolved.TargetPath;
                        }
                    }
                }

                // 2. Direct .ico or image file
                if (filePath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ||
                    filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    filePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(filePath))
                    {
                        var direct = LoadImageFromFile(filePath);
                        if (direct != null) return direct;
                    }
                }

                // 3. ExtractAssociatedIcon from GDI+
                if (File.Exists(filePath))
                {
                    try
                    {
                        using var gdiIcon = System.Drawing.Icon.ExtractAssociatedIcon(filePath);
                        if (gdiIcon != null)
                        {
                            var img = Imaging.CreateBitmapSourceFromHIcon(
                                gdiIcon.Handle,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                            img.Freeze();
                            return img;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Failed extracting associated icon for {Path}, falling back to SHGetFileInfo", filePath);
                    }
                }

                // 4. SHGetFileInfo fallback
                return GetShellIcon(filePath);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to extract icon for {Path}", filePath);
                return null;
            }
        }

        private ImageSource? GetShellIcon(string path)
        {
            var shinfo = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_LARGEICON;

            if (!File.Exists(path) && !Directory.Exists(path))
            {
                flags |= SHGFI_USEFILEATTRIBUTES;
            }

            IntPtr hImg = SHGetFileInfo(path, 0, out shinfo, (uint)Marshal.SizeOf(shinfo), flags);
            if (shinfo.hIcon != IntPtr.Zero)
            {
                try
                {
                    var img = Imaging.CreateBitmapSourceFromHIcon(
                        shinfo.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    img.Freeze();
                    return img;
                }
                finally
                {
                    DestroyIcon(shinfo.hIcon);
                }
            }
            return null;
        }

        public ImageSource? LoadImageFromFile(string path)
        {
            try
            {
                if (path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    BitmapFrame? bestFrame = null;
                    int maxRes = 0;
                    foreach (var frame in decoder.Frames)
                    {
                        int res = frame.PixelWidth * frame.PixelHeight;
                        if (res > maxRes)
                        {
                            maxRes = res;
                            bestFrame = frame;
                        }
                    }
                    if (bestFrame != null)
                    {
                        bestFrame.Freeze();
                        return bestFrame;
                    }
                }

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
                Log.Debug("Direct image load failed for {Path}: {Message}", path, ex.Message);
                return null;
            }
        }

        public class ShortcutInfo
        {
            public string TargetPath { get; set; } = string.Empty;
            public string IconLocation { get; set; } = string.Empty;
        }

        private ShortcutInfo? ResolveShortcut(string lnkPath)
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return null;
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(lnkPath);
                string target = shortcut.TargetPath ?? "";
                string iconLoc = shortcut.IconLocation ?? "";

                if (iconLoc.Contains(","))
                {
                    iconLoc = iconLoc.Split(',')[0].Trim();
                }

                return new ShortcutInfo
                {
                    TargetPath = target,
                    IconLocation = iconLoc
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
