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
        public void CalculateMagneticHoverOffset_ReturnsZeroWhenFar()
        {
            Point itemCenter = new Point(100, 100);
            Point mousePos = new Point(500, 500);

            var offset = RadialLayoutCalculator.CalculateMagneticHoverOffset(itemCenter, mousePos);

            Assert.Equal(0, offset.X);
            Assert.Equal(0, offset.Y);
        }
    }
}
