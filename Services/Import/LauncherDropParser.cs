using System;
using System.IO;
using System.Runtime.InteropServices;
using RadialLauncher.Models;
using Serilog;

namespace RadialLauncher.Services.Import
{
    public static class LauncherDropParser
    {
        public const string TypeExe = "EXE";
        public const string TypeUrl = "URL";
        public const string TypeFile = "FILE";
        public const string TypeFolder = "FOLDER";

        public static (bool Ok, string Message, LauncherItem? Item) BuildItem(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return (false, "Invalid path.", null);

            path = path.Trim();

            try
            {
                if (Directory.Exists(path))
                {
                    string dirName = new DirectoryInfo(path).Name;
                    return Make(TypeFolder, path, dirName, path);
                }

                if (!File.Exists(path))
                    return (false, $"File not found: {path}", null);

                string ext = Path.GetExtension(path);
                string name = Path.GetFileNameWithoutExtension(path);

                if (ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    string? target = ResolveShortcutTarget(path);
                    string targetPath = !string.IsNullOrEmpty(target) && (File.Exists(target) || Directory.Exists(target)) ? target : path;
                    return Make(TypeExe, targetPath, name, path);
                }

                if (ext.Equals(".url", StringComparison.OrdinalIgnoreCase))
                {
                    string url = ReadUrlTarget(path);
                    if (string.IsNullOrWhiteSpace(url))
                        return (false, $"No valid URL found in '{Path.GetFileName(path)}'.", null);
                    return Make(TypeUrl, url, name, string.Empty);
                }

                if (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".ps1", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".com", StringComparison.OrdinalIgnoreCase))
                {
                    return Make(TypeExe, path, name, path);
                }

                return Make(TypeFile, path, name, path);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "LauncherDropParser failed for {Path}", path);
                return (false, ex.Message, null);
            }
        }

        private static (bool, string, LauncherItem?) Make(string type, string target, string name, string iconPath)
        {
            return (true, string.Empty, new LauncherItem
            {
                Name = name,
                Type = type,
                Target = target,
                IconPath = iconPath,
                IsUserAdded = true,
                ParentId = 0
            });
        }

        public static string ReadUrlTarget(string urlFilePath)
        {
            try
            {
                foreach (string line in File.ReadAllLines(urlFilePath))
                {
                    if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                        return line.Substring(4).Trim();
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed reading .url target from {Path}", urlFilePath);
            }
            return string.Empty;
        }

        private static string? ResolveShortcutTarget(string shortcutPath)
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return null;
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                string target = shortcut.TargetPath;
                Marshal.FinalReleaseComObject(shortcut);
                Marshal.FinalReleaseComObject(shell);
                return target;
            }
            catch
            {
                return null;
            }
        }
    }
}
