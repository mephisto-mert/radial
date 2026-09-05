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
        public const double DefaultBaseRadius = 185.0;
        public const double CompactBaseRadius = 160.0;
        public const double LabelWidth = 72.0;
        public const double LabelHeight = 26.0;

        public static List<RadialItemPlacement> CalculatePlacements(
            int itemCount,
            double centerCanvasX = 270.0,
            double centerCanvasY = 270.0,
            bool isCompact = false)
        {
            var result = new List<RadialItemPlacement>();
            if (itemCount <= 0) return result;

            // Adaptive item sizing & spacing based on count N
            double circleSize = isCompact ? 46.0 : (itemCount > 15 ? 44.0 : 52.0);
            double iconSize = isCompact ? 34.0 : (itemCount > 15 ? 32.0 : 40.0);

            // Radius scales slightly with item count to avoid crowding
            double baseRadius = isCompact ? CompactBaseRadius : DefaultBaseRadius;
            if (itemCount > 12)
            {
                baseRadius += (itemCount - 12) * 2.5;
            }

            double angleStep = (2 * Math.PI) / itemCount;
            double startAngle = -Math.PI / 2.0; // 12 o'clock position

            for (int i = 0; i < itemCount; i++)
            {
                double angle = startAngle + (i * angleStep);
                double angleDeg = (angle * 180.0 / Math.PI) % 360;

                double btnX = centerCanvasX + (baseRadius * Math.Cos(angle)) - (circleSize / 2.0);
                double btnY = centerCanvasY + (baseRadius * Math.Sin(angle)) - (circleSize / 2.0);

                // Math-derived dynamic label distance to guarantee zero overlap
                double labelDist = (circleSize / 2.0) + 12.0 +
                                   (LabelWidth / 2.0 * Math.Abs(Math.Cos(angle))) +
                                   (LabelHeight / 2.0 * Math.Abs(Math.Sin(angle)));

                double lblX = centerCanvasX + ((baseRadius + labelDist) * Math.Cos(angle)) - (LabelWidth / 2.0);
                double lblY = centerCanvasY + ((baseRadius + labelDist) * Math.Sin(angle)) - (LabelHeight / 2.0);

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
    }
}
