using System;
using System.Collections.Generic;
using System.IO;
using RadialLauncher.Models;

namespace RadialLauncher.Services.Plugins
{
    public class OpenSourceToolsProvider : IRadialItemProvider
    {
        private readonly string _toolsRoot;

        public OpenSourceToolsProvider(string? toolsRoot = null)
        {
            _toolsRoot = !string.IsNullOrWhiteSpace(toolsRoot)
                ? toolsRoot
                : ResolveDefaultToolsRoot();
        }

        public string ProviderName => "OpenSourceTools";
        public string CategoryName => "🛠️ Open Source Tools";
        public string CategoryColor => "#16a085";
        public string ToolsRoot => _toolsRoot;

        private static string ResolveDefaultToolsRoot()
        {
            string candidate = @"c:\Users\pc\Documents\projelerim\open-source-tools";
            if (Directory.Exists(candidate)) return candidate;

            string userDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string fallback = Path.Combine(userDocs, "projelerim", "open-source-tools");
            return fallback;
        }

        public IEnumerable<LauncherItem> GetItems()
        {
            string launchBat = Path.Combine(_toolsRoot, "launch_tools.bat");
            string deepseekDir = Path.Combine(_toolsRoot, "deepseek-ocr.rs");
            string munderBat = Path.Combine(_toolsRoot, "munder-difflin", "start_munder_difflin.bat");
            string motionHtml = Path.Combine(_toolsRoot, "motion-skills", "showcase.html");
            string gcsfScript = Path.Combine(_toolsRoot, "gcsf_wsl_mount.ps1");
            string waScript = Path.Combine(_toolsRoot, "whatsapp_bridge_status.ps1");

            return new List<LauncherItem>
            {
                new LauncherItem
                {
                    Id = -601,
                    Name = "OpenSourceTools Hub",
                    Type = "EXE",
                    Target = File.Exists(launchBat) ? launchBat : "cmd.exe",
                    Arguments = File.Exists(launchBat) ? "" : $"/k cd /d \"{_toolsRoot}\"",
                    WorkingDirectory = _toolsRoot,
                    Tags = "open-source-tools hub launcher",
                    Position = 0
                },
                new LauncherItem
                {
                    Id = -602,
                    Name = "DeepSeek-OCR (Rust)",
                    Type = "EXE",
                    Target = "cmd.exe",
                    Arguments = $"/k cd /d \"{deepseekDir}\" && cargo --version && echo DeepSeek-OCR Ready",
                    WorkingDirectory = deepseekDir,
                    Tags = "deepseek-ocr ocr rust candle",
                    Position = 1
                },
                new LauncherItem
                {
                    Id = -603,
                    Name = "Munder Difflin (Multi-Agent)",
                    Type = "EXE",
                    Target = File.Exists(munderBat) ? munderBat : "explorer.exe",
                    Arguments = File.Exists(munderBat) ? "" : $"\"{Path.Combine(_toolsRoot, "munder-difflin")}\"",
                    WorkingDirectory = Path.Combine(_toolsRoot, "munder-difflin"),
                    Tags = "munder-difflin agents claude codex",
                    Position = 2
                },
                new LauncherItem
                {
                    Id = -604,
                    Name = "Motion Skills Studio",
                    Type = "FILE",
                    Target = motionHtml,
                    WorkingDirectory = Path.Combine(_toolsRoot, "motion-skills"),
                    Tags = "motion-skills kinetic typography manim",
                    Position = 3
                },
                new LauncherItem
                {
                    Id = -605,
                    Name = "GCSF Drive Mount (WSL2)",
                    Type = "EXE",
                    Target = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{gcsfScript}\"",
                    WorkingDirectory = _toolsRoot,
                    Tags = "gcsf google-drive fuse wsl2",
                    Position = 4
                },
                new LauncherItem
                {
                    Id = -606,
                    Name = "WhatsApp Bridge Status",
                    Type = "EXE",
                    Target = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{waScript}\"",
                    WorkingDirectory = _toolsRoot,
                    Tags = "go-whatsapp wa-akg bridge",
                    Position = 5
                }
            };
        }
    }
}
