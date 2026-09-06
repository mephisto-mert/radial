using System;
using System.IO;
using RadialLauncher.Models;
using RadialLauncher.Services.Import;
using Xunit;

namespace RadialLauncher.Tests
{
    public class LauncherDropParserTests : IDisposable
    {
        private readonly string _tempDir;

        public LauncherDropParserTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"drop_parser_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); }
            catch { /* best effort */ }
        }

        private string WriteFile(string name, string content = "")
        {
            string path = Path.Combine(_tempDir, name);
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public void Exe_BuildsExeItem()
        {
            string path = WriteFile("tool.exe", "x");

            var (ok, _, item) = LauncherDropParser.BuildItem(path);

            Assert.True(ok);
            Assert.NotNull(item);
            Assert.Equal("EXE", item!.Type);
            Assert.Equal("tool", item.Name);
            Assert.Equal(path, item.Target);
            Assert.Equal(path, item.IconPath);
            Assert.True(item.IsUserAdded);
        }

        [Fact]
        public void BatchAndCmd_BuildExeItems()
        {
            string cmd = WriteFile("setup.cmd", "@echo off");

            var (ok1, _, item1) = LauncherDropParser.BuildItem(cmd);

            Assert.True(ok1);
            Assert.Equal("EXE", item1!.Type);
        }

        [Fact]
        public void Folder_BuildsFolderItem()
        {
            string folder = Path.Combine(_tempDir, "Oyunlar");
            Directory.CreateDirectory(folder);

            var (ok, _, item) = LauncherDropParser.BuildItem(folder);

            Assert.True(ok);
            Assert.Equal("FOLDER", item!.Type);
            Assert.Equal("Oyunlar", item.Name);
            Assert.Equal(folder, item.Target);
        }

        [Fact]
        public void Lnk_BuildsExeItemTargetingShortcutFile()
        {
            string lnk = WriteFile("Notepad.lnk", "dummy");

            var (ok, _, item) = LauncherDropParser.BuildItem(lnk);

            Assert.True(ok);
            Assert.Equal("EXE", item!.Type);
            Assert.Equal("Notepad", item.Name);
            Assert.Equal(lnk, item.Target);
            Assert.Equal(lnk, item.IconPath);
        }

        [Fact]
        public void UrlFile_BuildsUrlItem()
        {
            string urlFile = WriteFile("Açık kaynak.url", "[InternetShortcut]\nURL=https://example.com\n");

            var (ok, _, item) = LauncherDropParser.BuildItem(urlFile);

            Assert.True(ok);
            Assert.Equal("URL", item!.Type);
            Assert.Equal("Açık kaynak", item.Name);
            Assert.Equal("https://example.com", item.Target);
        }

        [Fact]
        public void UrlFileWithoutUrlTarget_ReturnsNotOk()
        {
            string urlFile = WriteFile("bozuk.url", "[InternetShortcut]\nIconFile=foo.ico\n");

            var (ok, _, item) = LauncherDropParser.BuildItem(urlFile);

            Assert.False(ok);
            Assert.Null(item);
        }

        [Fact]
        public void PlainTextFile_BuildsFileItem()
        {
            string path = WriteFile("notlar.txt", "selam");

            var (ok, _, item) = LauncherDropParser.BuildItem(path);

            Assert.True(ok);
            Assert.Equal("FILE", item!.Type);
            Assert.Equal(path, item.Target);
        }

        [Fact]
        public void MissingPath_ReturnsNotOk()
        {
            string missing = Path.Combine(_tempDir, "yok.exe");

            var (ok, _, item) = LauncherDropParser.BuildItem(missing);

            Assert.False(ok);
            Assert.Null(item);
        }

        [Fact]
        public void NullOrWhitespacePath_ReturnsNotOk()
        {
            Assert.False(LauncherDropParser.BuildItem("").Ok);
            Assert.False(LauncherDropParser.BuildItem("   ").Ok);
        }
    }
}
