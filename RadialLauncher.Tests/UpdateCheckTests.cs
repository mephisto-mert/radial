using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using RadialLauncher.Services.Updates;
using Xunit;

namespace RadialLauncher.Tests
{
    public class UpdateCheckTests
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
        public async Task CheckForUpdatesAsync_WhenNewerVersionExists_ReturnsUpdateAvailable()
        {
            string json = "{\"tag_name\":\"v99.0.0\",\"body\":\"Bug fixes and new features\",\"html_url\":\"https://github.com/mephisto-mert/radial/releases/tag/v99.0.0\"}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };

            var factory = new MockHttpClientFactory(response);
            var service = new UpdateCheckService(factory);

            var result = await service.CheckForUpdatesAsync();

            Assert.NotNull(result);
            Assert.True(result.IsUpdateAvailable);
            Assert.Equal("99.0.0", result.LatestVersion);
            Assert.Equal("https://github.com/mephisto-mert/radial/releases/tag/v99.0.0", result.ReleaseUrl);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_WhenOlderOrSameVersion_ReturnsNotAvailable()
        {
            string json = "{\"tag_name\":\"v1.0.0\",\"body\":\"Initial release\",\"html_url\":\"https://github.com/mephisto-mert/radial/releases/tag/v1.0.0\"}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };

            var factory = new MockHttpClientFactory(response);
            var service = new UpdateCheckService(factory);

            var result = await service.CheckForUpdatesAsync();

            Assert.NotNull(result);
            Assert.False(result.IsUpdateAvailable);
            Assert.Equal("1.0.0", result.LatestVersion);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_WhenHttpError_ReturnsNull()
        {
            var response = new HttpResponseMessage(HttpStatusCode.NotFound);
            var factory = new MockHttpClientFactory(response);
            var service = new UpdateCheckService(factory);

            var result = await service.CheckForUpdatesAsync();

            Assert.Null(result);
        }
    }
}