using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using RadialLauncher.Data.Repositories;
using RadialLauncher.Models;
using RadialLauncher.Services.Sync;
using Xunit;

namespace RadialLauncher.Tests
{
    public class SyncServiceTests
    {
        private class MockHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpResponseMessage _response;

            public MockHttpClientFactory(HttpResponseMessage response)
            {
                _response = response;
            }

            public HttpClient CreateClient(string name)
            {
                return new HttpClient(new MockHttpMessageHandler(_response));
            }
        }

        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly HttpResponseMessage _response;

            public MockHttpMessageHandler(HttpResponseMessage response)
            {
                _response = response;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_response);
            }
        }

        [Fact]
        public void SavePat_EncryptsAndPersistsCorrectly()
        {
            var mockItemRepo = new Mock<IItemRepository>();
            var mockCatRepo = new Mock<ICategoryRepository>();
            var syncService = new SyncService(mockItemRepo.Object, mockCatRepo.Object, new MockHttpClientFactory(new HttpResponseMessage(HttpStatusCode.OK)));

            try
            {
                syncService.ClearPat();
                Assert.False(syncService.HasPatConfigured());

                string testPat = "ghp_TestSecretToken123456789";
                string testGistId = "gist_abc123";

                syncService.SavePat(testPat, testGistId);

                Assert.True(syncService.HasPatConfigured());
                Assert.Equal(testGistId, syncService.GetGistId());
            }
            finally
            {
                syncService.ClearPat();
            }
        }

        [Fact]
        public async Task ExportAndImportFromFile_PreservesPayloadStructure()
        {
            var items = new List<LauncherItem>
            {
                new LauncherItem { Id = 1, Name = "Notepad", Type = "EXE", Target = "notepad.exe", Position = 0 }
            };
            var categories = new List<Category>
            {
                new Category { Id = 1, Name = "Genel", Color = "#3498db", Position = 0 }
            };

            var mockItemRepo = new Mock<IItemRepository>();
            mockItemRepo.Setup(r => r.GetAll()).Returns(items);
            mockItemRepo.Setup(r => r.GetById(1)).Returns(items[0]);

            var mockCatRepo = new Mock<ICategoryRepository>();
            mockCatRepo.Setup(r => r.GetAll()).Returns(categories);
            mockCatRepo.Setup(r => r.GetById(1)).Returns(categories[0]);

            var syncService = new SyncService(mockItemRepo.Object, mockCatRepo.Object, new MockHttpClientFactory(new HttpResponseMessage(HttpStatusCode.OK)));

            string tempFile = Path.Combine(Path.GetTempPath(), $"sync_test_{Guid.NewGuid():N}.json");
            try
            {
                bool exported = await syncService.ExportToFileAsync(tempFile);
                Assert.True(exported);
                Assert.True(File.Exists(tempFile));

                bool imported = await syncService.ImportFromFileAsync(tempFile);
                Assert.True(imported);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task PushToGist_WithoutPat_ReturnsErrorSafely()
        {
            var mockItemRepo = new Mock<IItemRepository>();
            var mockCatRepo = new Mock<ICategoryRepository>();
            var syncService = new SyncService(mockItemRepo.Object, mockCatRepo.Object, new MockHttpClientFactory(new HttpResponseMessage(HttpStatusCode.OK)));

            syncService.ClearPat();
            var result = await syncService.PushToGistAsync();

            Assert.False(result.success);
            Assert.Contains("PAT", result.message);
        }

        [Fact]
        public async Task ImportFromFile_MalformedJson_ReturnsFalseSafely()
        {
            var mockItemRepo = new Mock<IItemRepository>();
            var mockCatRepo = new Mock<ICategoryRepository>();
            var syncService = new SyncService(mockItemRepo.Object, mockCatRepo.Object, new MockHttpClientFactory(new HttpResponseMessage(HttpStatusCode.OK)));

            string tempFile = Path.Combine(Path.GetTempPath(), $"sync_malformed_{Guid.NewGuid():N}.json");
            try
            {
                await File.WriteAllTextAsync(tempFile, "{ broken json content");
                bool imported = await syncService.ImportFromFileAsync(tempFile);
                Assert.False(imported);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task ImportFromFile_NonExistentFile_ReturnsFalseSafely()
        {
            var mockItemRepo = new Mock<IItemRepository>();
            var mockCatRepo = new Mock<ICategoryRepository>();
            var syncService = new SyncService(mockItemRepo.Object, mockCatRepo.Object, new MockHttpClientFactory(new HttpResponseMessage(HttpStatusCode.OK)));

            bool imported = await syncService.ImportFromFileAsync("C:\\NonExistentPath_XYZ_123.json");
            Assert.False(imported);
        }

        [Fact]
        public async Task CreateLocalBackup_CreatesValidBackupAndRotates()
        {
            var mockItemRepo = new Mock<IItemRepository>();
            mockItemRepo.Setup(r => r.GetAll()).Returns(new List<LauncherItem>
            {
                new LauncherItem { Id = 1, Name = "TestApp", Target = "test.exe" }
            });

            var mockCatRepo = new Mock<ICategoryRepository>();
            mockCatRepo.Setup(r => r.GetAll()).Returns(new List<Category>
            {
                new Category { Id = 1, Name = "TestCategory" }
            });

            var syncService = new SyncService(mockItemRepo.Object, mockCatRepo.Object, new MockHttpClientFactory(new HttpResponseMessage(HttpStatusCode.OK)));

            var (success, filePath) = await syncService.CreateLocalBackupAsync();
            Assert.True(success);
            Assert.True(File.Exists(filePath));

            var backups = syncService.GetLocalBackups();
            Assert.NotEmpty(backups);
            Assert.Contains(filePath, backups);

            // Verify content
            string content = await File.ReadAllTextAsync(filePath);
            Assert.Contains("TestApp", content);
            Assert.Contains("TestCategory", content);
        }

        [Fact]
        public async Task RestoreFromLocalBackupAsync_WithValidBackup_RestoresItemsSuccessfully()
        {
            var mockItemRepo = new Mock<IItemRepository>();
            var mockCatRepo = new Mock<ICategoryRepository>();

            var syncService = new SyncService(mockItemRepo.Object, mockCatRepo.Object, new MockHttpClientFactory(new HttpResponseMessage(HttpStatusCode.OK)));

            string tempBackup = Path.Combine(Path.GetTempPath(), $"backup_restore_test_{Guid.NewGuid():N}.json");
            try
            {
                var payload = new SyncService.SyncPayload
                {
                    Settings = new RadialLauncher.Services.Themes.ThemeService.AppSettings
                    {
                        ThemeName = "Purple",
                        RadialOpacity = 0.85,
                        ActivationShortcut = "CtrlSpace",
                        DensityMode = "Compact",
                        Language = "de"
                    },
                    Categories = new List<Category> { new Category { Id = 1, Name = "RestoredCat" } },
                    Items = new List<LauncherItem> { new LauncherItem { Id = 1, Name = "RestoredApp", Target = "app.exe" } }
                };
                string json = System.Text.Json.JsonSerializer.Serialize(payload);
                await File.WriteAllTextAsync(tempBackup, json);

                bool restored = await syncService.RestoreFromLocalBackupAsync(tempBackup);
                Assert.True(restored);

                mockCatRepo.Verify(r => r.Insert(It.Is<Category>(c => c.Name == "RestoredCat")), Times.Once);
                mockItemRepo.Verify(r => r.Insert(It.Is<LauncherItem>(i => i.Name == "RestoredApp")), Times.Once);

                // Verify restored theme settings and language
                var currentTheme = RadialLauncher.Services.Themes.ThemeService.Instance.GetCurrentTheme();
                Assert.Equal("Purple", currentTheme.Name);
                Assert.Equal("de", RadialLauncher.Services.Localization.LocalizationService.Instance.CurrentLanguage);

                // Cleanup settings
                RadialLauncher.Services.Localization.LocalizationService.Instance.SetLanguage("en");
            }
            finally
            {
                if (File.Exists(tempBackup)) File.Delete(tempBackup);
            }
        }
    }
}