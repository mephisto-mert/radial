using System;
using System.Collections.Generic;
using System.Windows;

namespace RadialLauncher.UI.Helpers
{
    public class RadialItemPlacement
    {
        public int Index { get; set; }
        public double AngleRadians { get; set; }
        public double AngleDegrees { get; set; }
        public double ButtonX { get; set; }
        public double ButtonY { get; set; }
        public double LabelX { get; set; }
        public double LabelY { get; set; }
        public double CircleSize { get; set; }
        public double IconSize { get; set; }
    }

    public static class RadialLayoutCalculator
    {
        public const double DefaultBaseRadius = 175.0;
        public const double CompactBaseRadius = 150.0;
        public const double LabelWidth = 64.0;
        public const double LabelHeight = 20.0;

        public static List<RadialItemPlacement> CalculatePlacements(
            int itemCount,
            double centerCanvasX = 250.0,
            double centerCanvasY = 250.0,
            bool isCompact = false)
        {
            var result = new List<RadialItemPlacement>();
            if (itemCount <= 0) return result;

            // Refined, sleek item sizing & spacing
            double circleSize = isCompact ? 42.0 : (itemCount > 15 ? 42.0 : 48.0);
            double iconSize = isCompact ? 24.0 : (itemCount > 15 ? 24.0 : 28.0);

            // Radius scales slightly with item count to avoid crowding
            double baseRadius = isCompact ? CompactBaseRadius : DefaultBaseRadius;
            if (itemCount > 12)
            {
                baseRadius += (itemCount - 12) * 2.0;
            }

            double angleStep = (2 * Math.PI) / itemCount;
            double startAngle = -Math.PI / 2.0; // 12 o'clock position

            for (int i = 0; i < itemCount; i++)
            {
                double angle = startAngle + (i * angleStep);
                double angleDeg = (angle * 180.0 / Math.PI) % 360;

                double btnX = centerCanvasX + (baseRadius * Math.Cos(angle)) - (circleSize / 2.0);
                double btnY = centerCanvasY + (baseRadius * Math.Sin(angle)) - (circleSize / 2.0);

                // Place label badge centered directly below the icon button
                double lblX = btnX + (circleSize - LabelWidth) / 2.0;
                double lblY = btnY + circleSize + 2.0;

                result.Add(new RadialItemPlacement
                {
                    Index = i,
                    AngleRadians = angle,
                    AngleDegrees = angleDeg,
                    ButtonX = btnX,
                    ButtonY = btnY,
                    LabelX = lblX,
                    LabelY = lblY,
                    CircleSize = circleSize,
                    IconSize = iconSize
                });
            }

            return result;
        }

        public static Point CalculateMagneticHoverOffset(Point itemCenter, Point mousePos, double maxPullDistance = 8.0)
        {
            double dx = mousePos.X - itemCenter.X;
            double dy = mousePos.Y - itemCenter.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist < 0.001 || dist > 60.0) return new Point(0, 0);

            double factor = (1.0 - (dist / 60.0)) * maxPullDistance;
            return new Point((dx / dist) * factor, (dy / dist) * factor);
        }

        public static int CalculateNearestSlot(Point mousePos, int itemCount, double centerCanvasX = 250.0, double centerCanvasY = 250.0)
        {
            if (itemCount <= 1) return 0;

            double dx = mousePos.X - centerCanvasX;
            double dy = mousePos.Y - centerCanvasY;
            double currentAngle = Math.Atan2(dy, dx);

            // Start angle is -PI / 2.0 (12 o'clock)
            double diff = currentAngle - (-Math.PI / 2.0);
            while (diff < 0) diff += 2 * Math.PI;
            while (diff >= 2 * Math.PI) diff -= 2 * Math.PI;

            double angleStep = (2 * Math.PI) / itemCount;
            int slot = (int)Math.Round(diff / angleStep) % itemCount;
            if (slot < 0) slot += itemCount;

            return slot;
        }
    }
}
