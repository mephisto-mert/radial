using System;
using RadialLauncher.UI.Animations;
using Xunit;

namespace RadialLauncher.Tests
{
    public class VelocityAnimationTests
    {
        [Fact]
        public void SlowOrStaticCursor_UsesFullDurationScale()
        {
            double scaleZero = RadialMotionSystem.CalculateDurationScale(0.0);
            double scaleSlow = RadialMotionSystem.CalculateDurationScale(0.15);

            Assert.Equal(1.0, scaleZero);
            Assert.Equal(1.0, scaleSlow);
        }

        [Fact]
        public void FastFlickCursor_ScalesToSnappyFactor()
        {
            double scaleFast = RadialMotionSystem.CalculateDurationScale(2.0);
            double scaleExtreme = RadialMotionSystem.CalculateDurationScale(10.0);

            Assert.Equal(0.4, scaleFast);
            Assert.Equal(0.4, scaleExtreme); // Clamped at 0.4
        }

        [Fact]
        public void ModerateVelocity_InterpolatesSmoothlyWithin2To3xRange()
        {
            double scaleMid = RadialMotionSystem.CalculateDurationScale(1.1);

            Assert.True(scaleMid > 0.4 && scaleMid < 1.0);

            // Ratio of slowest to fastest must be within 2.0x to 3.0x
            double slowest = RadialMotionSystem.CalculateDurationScale(0.0);
            double fastest = RadialMotionSystem.CalculateDurationScale(5.0);
            double ratio = slowest / fastest;

            Assert.True(ratio >= 2.0 && ratio <= 3.0, $"Ratio {ratio} is not within 2-3x range");
        }
    }
}
