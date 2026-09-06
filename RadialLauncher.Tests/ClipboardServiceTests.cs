using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RadialLauncher.Services.Clipboard;
using Xunit;

namespace RadialLauncher.Tests
{
    public class ClipboardServiceTests
    {
        [Fact]
        public void AddToHistory_CapsAtMaxHistory_AndMaintainsRollingOrder()
        {
            var service = new ClipboardService();

            for (int i = 1; i <= 25; i++)
            {
                service.AddToHistory($"Item {i}");
            }

            var history = service.GetRecentHistory(50);

            Assert.Equal(20, history.Count);
            // Most recent is "Item 25"
            Assert.Equal("Item 25", history[0].Text);
            // Oldest retained is "Item 6"
            Assert.Equal("Item 6", history[^1].Text);
        }

        [Fact]
        public void AddToHistory_DeduplicatesExistingItem_AndBringsToTop()
        {
            var service = new ClipboardService();

            service.AddToHistory("First");
            service.AddToHistory("Second");
            service.AddToHistory("Third");

            // Re-add First
            service.AddToHistory("First");

            var history = service.GetRecentHistory(10);

            Assert.Equal(3, history.Count);
            Assert.Equal("First", history[0].Text);
            Assert.Equal("Third", history[1].Text);
            Assert.Equal("Second", history[2].Text);
        }

        [Fact]
        public void AddToHistory_IgnoresNullOrWhitespace()
        {
            var service = new ClipboardService();

            service.AddToHistory(null!);
            service.AddToHistory("");
            service.AddToHistory("   ");

            var history = service.GetRecentHistory(10);
            Assert.Empty(history);
        }

        [Fact]
        public void AddToHistory_UnicodeAndTurkishCharacters_PreservedCorrectly()
        {
            var service = new ClipboardService();

            string turkishText = "Türkçe Karakterler: ğüşıöç ĞÜŞİÖÇ 🌟 🚀";
            service.AddToHistory(turkishText);

            var history = service.GetRecentHistory(10);
            Assert.Single(history);
            Assert.Equal(turkishText, history[0].Text);
        }

        [Fact]
        public void AddToHistory_ConcurrentAccess_IsThreadSafeAndRespectsCap()
        {
            var service = new ClipboardService();

            Parallel.For(0, 100, i =>
            {
                service.AddToHistory($"Concurrent Item {i}");
            });

            var history = service.GetRecentHistory(50);
            Assert.Equal(20, history.Count);
        }

        [Fact]
        public void Listener_RepeatedStartAndStop_DoesNotThrow()
        {
            var service = new ClipboardService();

            var ex1 = Record.Exception(() =>
            {
                service.StartListening(new IntPtr(100));
                service.StartListening(new IntPtr(100)); // Repeated start
                service.StopListening(new IntPtr(100));
                service.StopListening(new IntPtr(100));  // Repeated stop
                service.StopListening(IntPtr.Zero);
            });

            Assert.Null(ex1);
        }
    }
}