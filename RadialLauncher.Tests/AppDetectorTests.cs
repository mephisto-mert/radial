using System;
using System.Collections.Generic;
using System.Linq;
using RadialLauncher.Services.Apps;
using Xunit;

namespace RadialLauncher.Tests
{
    public class AppDetectorTests
    {
        [Fact]
        public void DetectRunningAndCommonApps_DoesNotThrow_AndReturnsApps()
        {
            var detector = new AppDetector();
            var apps = detector.DetectRunningAndCommonApps();

            Assert.NotNull(apps);
            // Verify no system internal executables in the results
            Assert.DoesNotContain(apps, a => a.ExePath.Contains(@"\Windows\System32", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(apps, a => a.ExePath.Contains(@"\Windows\SysWOW64", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(apps, a => a.Name.Equals("svchost", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(apps, a => a.Name.Equals("dwm", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void DetectedApp_HasValidDefaultCategoryKey()
        {
            var app = new DetectedApp
            {
                Name = "Google Chrome",
                ExePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe"
            };

            Assert.Equal("Cat_Apps", app.CategoryKey);
            Assert.False(string.IsNullOrWhiteSpace(app.CategoryName));
        }
    }
}
