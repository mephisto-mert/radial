using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;

namespace RadialLauncher.UI.Helpers
{
    public static class MultiMonitorHelper
    {
        public static Point ClampWindowToCursorScreen(
            double windowWidth,
            double windowHeight,
            double cursorLogicalX,
            double cursorLogicalY,
            double dpiX = 1.0,
            double dpiY = 1.0)
        {
            if (dpiX <= 0) dpiX = 1.0;
            if (dpiY <= 0) dpiY = 1.0;

            // Center window initially around cursor (in logical DIPs)
            double left = cursorLogicalX - (windowWidth / 2.0);
            double top = cursorLogicalY - (windowHeight / 2.0);

            try
            {
                int physX = (int)Math.Round(cursorLogicalX * dpiX);
                int physY = (int)Math.Round(cursorLogicalY * dpiY);
                var screen = Screen.FromPoint(new System.Drawing.Point(physX, physY));
                var bounds = screen.WorkingArea;

                double minLeft = bounds.Left / dpiX;
                double minTop = bounds.Top / dpiY;
                double maxRight = bounds.Right / dpiX;
                double maxBottom = bounds.Bottom / dpiY;

                // Clamp to working area in logical DIPs
                if (left < minLeft) left = minLeft;
                if (top < minTop) top = minTop;
                if (left + windowWidth > maxRight) left = maxRight - windowWidth;
                if (top + windowHeight > maxBottom) top = maxBottom - windowHeight;
            }
            catch
            {
                if (left < 0) left = 0;
                if (top < 0) top = 0;
            }

            return new Point(left, top);
        }
    }
}
