using System;
using System.Collections.Generic;
using System.Windows;
using RadialLauncher.UI.Helpers;
using Xunit;

namespace RadialLauncher.Tests
{
    public class RadialLayoutCalculatorTests
    {
        [Theory]
        [InlineData(6, false)]
        [InlineData(8, false)]
        [InlineData(15, false)]
        [InlineData(18, false)]
        [InlineData(8, true)]
        public void CalculatePlacements_GeneratesValidPlacementsForEachItem(int count, bool isCompact)
        {
            double centerX = 270.0;
            double centerY = 270.0;

            var placements = RadialLayoutCalculator.CalculatePlacements(count, centerX, centerY, isCompact);

            Assert.Equal(count, placements.Count);

            for (int i = 0; i < count; i++)
            {
                var p = placements[i];
                Assert.Equal(i, p.Index);
                Assert.True(p.CircleSize > 0);
                Assert.True(p.IconSize > 0);
                Assert.True(p.ButtonX != 0 || p.ButtonY != 0);

                // Verify label is centered beneath the button
                double expectedLabelX = p.ButtonX + (p.CircleSize - RadialLayoutCalculator.LabelWidth) / 2.0;
                double expectedLabelY = p.ButtonY + p.CircleSize + 2.0;
                Assert.Equal(expectedLabelX, p.LabelX, 3);
                Assert.Equal(expectedLabelY, p.LabelY, 3);
            }
        }

        [Fact]
        public void CalculatePlacements_ExpandsRadiusForLargeItemCounts()
        {
            var smallList = RadialLayoutCalculator.CalculatePlacements(8, 270, 270, false);
            var largeList = RadialLayoutCalculator.CalculatePlacements(18, 270, 270, false);

            // Compute distance from center for first item in both
            double dSmall = Math.Sqrt(Math.Pow(smallList[0].ButtonX + smallList[0].CircleSize/2 - 270, 2) +
                                      Math.Pow(smallList[0].ButtonY + smallList[0].CircleSize/2 - 270, 2));

            double dLarge = Math.Sqrt(Math.Pow(largeList[0].ButtonX + largeList[0].CircleSize/2 - 270, 2) +
                                      Math.Pow(largeList[0].ButtonY + largeList[0].CircleSize/2 - 270, 2));

            Assert.True(dLarge > dSmall, $"Expected large radius ({dLarge}) > small radius ({dSmall})");
        }

        [Fact]
        public void CalculatePlacements_ZeroOrNegativeCount_ReturnsEmptyList()
        {
            var emptyZero = RadialLayoutCalculator.CalculatePlacements(0);
            var emptyNeg = RadialLayoutCalculator.CalculatePlacements(-5);

            Assert.Empty(emptyZero);
            Assert.Empty(emptyNeg);
        }

        [Fact]
        public void CalculateMagneticHoverOffset_WhenClose_ReturnsNonZeroPullOffset()
        {
            Point itemCenter = new Point(100, 100);
            Point mousePos = new Point(120, 100); // 20px away, within 60px threshold

            var offset = RadialLayoutCalculator.CalculateMagneticHoverOffset(itemCenter, mousePos, 8.0);

            Assert.True(offset.X > 0);
            Assert.Equal(0, offset.Y);
        }

        [Fact]
        public void CalculateMagneticHoverOffset_ReturnsZeroWhenFar()
        {
            Point itemCenter = new Point(100, 100);
            Point mousePos = new Point(500, 500);

            var offset = RadialLayoutCalculator.CalculateMagneticHoverOffset(itemCenter, mousePos);

            Assert.Equal(0, offset.X);
            Assert.Equal(0, offset.Y);
        }

        [Theory]
        [InlineData(250.0, 50.0, 4, 0)]  // Top (12 o'clock)
        [InlineData(450.0, 250.0, 4, 1)] // Right (3 o'clock)
        [InlineData(250.0, 450.0, 4, 2)] // Bottom (6 o'clock)
        [InlineData(50.0, 250.0, 4, 3)]  // Left (9 o'clock)
        public void CalculateNearestSlot_4Items_ReturnsExpectedSlot(double mouseX, double mouseY, int count, int expectedSlot)
        {
            int slot = RadialLayoutCalculator.CalculateNearestSlot(new Point(mouseX, mouseY), count, 250.0, 250.0);
            Assert.Equal(expectedSlot, slot);
        }

        [Fact]
        public void CalculateNearestSlot_SingleOrZeroItems_ReturnsZero()
        {
            Assert.Equal(0, RadialLayoutCalculator.CalculateNearestSlot(new Point(100, 100), 1, 250.0, 250.0));
            Assert.Equal(0, RadialLayoutCalculator.CalculateNearestSlot(new Point(100, 100), 0, 250.0, 250.0));
        }
    }
}
