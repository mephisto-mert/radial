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
        public double DynamicRadius { get; set; }
        public double DynamicTileSize { get; set; }
        public double DynamicIconSize { get; set; }
        public double DynamicButtonX { get; set; }
        public double DynamicButtonY { get; set; }
    }

    public enum OuterPillSide
    {
        Top,
        Bottom,
        Left,
        Right
    }

    public static class RadialLayoutCalculator
    {
        public const double DefaultBaseRadius = 175.0;
        public const double CompactBaseRadius = 150.0;
        public const double LabelWidth = 64.0;
        public const double LabelHeight = 20.0;

        public static (double radius, double tileSize, double iconSize) ComputeRovylMetrics(int itemCount, bool isCompact = false)
        {
            if (itemCount <= 0) return (138.0, 56.0, 38.0);

            double density = Math.Clamp((itemCount - 3) / 8.0, 0.0, 1.0);
            double baseTile = isCompact ? 48.0 : 56.0;
            double tileSize = Math.Round(baseTile * (1.0 - 0.10 * density));
            if (itemCount > 10)
            {
                tileSize = Math.Max(46.0, Math.Round(tileSize - (itemCount - 10) * 1.0));
            }

            double sinHalf = itemCount > 1 ? Math.Sin(Math.PI / itemCount) : 1.0;
            double minGap = isCompact ? 16.0 : 22.0;
            double packedRadius = itemCount > 1 ? (tileSize + minGap) / (2.0 * sinHalf) : 122.0;
            double floorRadius = (isCompact ? 116.0 : 126.0) + (30.0 * density);

            double radius = Math.Clamp(Math.Max(floorRadius, packedRadius), 116.0, 188.0);
            double iconSize = Math.Round(tileSize * 0.68);
            return (radius, tileSize, iconSize);
        }

        public static System.Windows.Media.Color ExtractDominantAccentColor(
            System.Windows.Media.ImageSource? source,
            System.Windows.Media.Color fallback)
        {
            try
            {
                if (source is not System.Windows.Media.Imaging.BitmapSource bmp)
                    return fallback;

                var formatted = new System.Windows.Media.Imaging.FormatConvertedBitmap(
                    bmp,
                    System.Windows.Media.PixelFormats.Bgra32,
                    null,
                    0);
                int w = formatted.PixelWidth;
                int h = formatted.PixelHeight;
                if (w <= 0 || h <= 0) return fallback;

                int stride = w * 4;
                byte[] pixels = new byte[h * stride];
                formatted.CopyPixels(pixels, stride, 0);

                double sumR = 0, sumG = 0, sumB = 0, totalWeight = 0;
                int stepX = Math.Max(1, w / 16);
                int stepY = Math.Max(1, h / 16);

                for (int y = 0; y < h; y += stepY)
                {
                    int row = y * stride;
                    for (int x = 0; x < w; x += stepX)
                    {
                        int idx = row + x * 4;
                        byte b = pixels[idx];
                        byte g = pixels[idx + 1];
                        byte r = pixels[idx + 2];
                        byte a = pixels[idx + 3];
                        if (a < 140) continue;

                        double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
                        double max = Math.Max(rf, Math.Max(gf, bf));
                        double min = Math.Min(rf, Math.Min(gf, bf));
                        double delta = max - min;
                        double sat = max <= 0.01 ? 0 : delta / max;
                        if (sat < 0.25 || max < 0.22) continue;

                        double weight = sat * sat * max * (a / 255.0);
                        sumR += r * weight;
                        sumG += g * weight;
                        sumB += b * weight;
                        totalWeight += weight;
                    }
                }

                if (totalWeight < 0.4) return fallback;

                double avgR = sumR / totalWeight;
                double avgG = sumG / totalWeight;
                double avgB = sumB / totalWeight;

                // Boost brightness so the accent glow is vivid and vibrant
                double peak = Math.Max(avgR, Math.Max(avgG, avgB));
                if (peak > 1 && peak < 235)
                {
                    double boost = 235.0 / peak;
                    avgR = Math.Clamp(avgR * boost, 0, 255);
                    avgG = Math.Clamp(avgG * boost, 0, 255);
                    avgB = Math.Clamp(avgB * boost, 0, 255);
                }

                return System.Windows.Media.Color.FromRgb((byte)avgR, (byte)avgG, (byte)avgB);
            }
            catch
            {
                return fallback;
            }
        }

        public static OuterPillSide GetOuterPillSide(double angleRadians)
        {
            double cos = Math.Cos(angleRadians);
            double sin = Math.Sin(angleRadians);
            if (cos > 0.35) return OuterPillSide.Right;
            if (cos < -0.35) return OuterPillSide.Left;
            if (sin < 0) return OuterPillSide.Top;
            return OuterPillSide.Bottom;
        }

        public static List<RadialItemPlacement> CalculatePlacements(
            int itemCount,
            double centerCanvasX = 250.0,
            double centerCanvasY = 250.0,
            bool isCompact = false)
        {
            var result = new List<RadialItemPlacement>();
            if (itemCount <= 0) return result;

            // Refined, sleek item sizing & spacing (preserved for unit test contract)
            double circleSize = isCompact ? 42.0 : (itemCount > 15 ? 42.0 : 48.0);
            double iconSize = isCompact ? 24.0 : (itemCount > 15 ? 24.0 : 28.0);

            // Radius scales slightly with item count to avoid crowding
            double baseRadius = isCompact ? CompactBaseRadius : DefaultBaseRadius;
            if (itemCount > 12)
            {
                baseRadius += (itemCount - 12) * 2.0;
            }

            var (dynRadius, dynTileSize, dynIconSize) = ComputeRovylMetrics(itemCount, isCompact);

            double angleStep = (2 * Math.PI) / itemCount;
            double startAngle = -Math.PI / 2.0; // 12 o'clock position

            for (int i = 0; i < itemCount; i++)
            {
                double angle = startAngle + (i * angleStep);
                double angleDeg = (angle * 180.0 / Math.PI) % 360;

                double btnX = centerCanvasX + (baseRadius * Math.Cos(angle)) - (circleSize / 2.0);
                double btnY = centerCanvasY + (baseRadius * Math.Sin(angle)) - (circleSize / 2.0);

                double dynBtnX = Math.Round(centerCanvasX + (dynRadius * Math.Cos(angle)) - (dynTileSize / 2.0));
                double dynBtnY = Math.Round(centerCanvasY + (dynRadius * Math.Sin(angle)) - (dynTileSize / 2.0));

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
                    IconSize = iconSize,
                    DynamicRadius = dynRadius,
                    DynamicTileSize = dynTileSize,
                    DynamicIconSize = dynIconSize,
                    DynamicButtonX = dynBtnX,
                    DynamicButtonY = dynBtnY
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

        public static int ResolveAimSlot(
            Point mousePos, 
            int itemCount, 
            string aimMode = "angle", 
            double centerCanvasX = 250.0, 
            double centerCanvasY = 250.0, 
            double deadZoneRadius = 45.0,
            double outerRadius = 260.0)
        {
            if (itemCount <= 0) return -1;

            double dx = mousePos.X - centerCanvasX;
            double dy = mousePos.Y - centerCanvasY;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            // Inside dead zone: no selection / cancel
            if (dist < deadZoneRadius) return -1;

            // Outside outer limit: cancel
            if (dist > outerRadius) return -1;

            if (aimMode.Equals("cursor", StringComparison.OrdinalIgnoreCase))
            {
                // In cursor mode, must be in reasonable proximity to the icon ring radius (~130 - 230px)
                if (dist < 110.0 || dist > 235.0) return -1;
            }

            return CalculateNearestSlot(mousePos, itemCount, centerCanvasX, centerCanvasY);
        }
    }
}
