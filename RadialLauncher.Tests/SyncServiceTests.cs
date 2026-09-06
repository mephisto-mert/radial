using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
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
        private class DummyHttpClientFactory : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => new HttpClient();
        }

        [Fact]
        public void SavePat_EncryptsAndPersistsCorrectly()
        {
            var mockItemRepo = new Mock<IItemRepository>();
            var mockCatRepo = new Mock<ICategoryRepository>();
            var syncService = new SyncService(mockItemRepo.Object, mockCatRepo.Object, new DummyHttpClientFactory());

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

            var syncService = new SyncService(mockItemRepo.Object, mockCatRepo.Object, new DummyHttpClientFactory());

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
        public async Task PushToGist_WithoutPat_ReturnsError()
        {
            var mockItemRepo = new Mock<IItemRepository>();
            var mockCatRepo = new Mock<ICategoryRepository>();
            var syncService = new SyncService(mockItemRepo.Object, mockCatRepo.Object, new DummyHttpClientFactory());

            syncService.ClearPat();
            var result = await syncService.PushToGistAsync();

            Assert.False(result.success);
            Assert.Contains("PAT", result.message);
        }
    }
}
