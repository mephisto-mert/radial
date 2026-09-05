using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;

namespace RadialLauncher.UI.Helpers
{
    public static class MultiMonitorHelper
    {
        public static Point ClampWindowToCursorScreen(double windowWidth, double windowHeight, double cursorLogicalX, double cursorLogicalY)
        {
            // Center window initially around cursor
            double left = cursorLogicalX - (windowWidth / 2.0);
            double top = cursorLogicalY - (windowHeight / 2.0);

            try
            {
                // Find screen containing cursor
                var screen = Screen.FromPoint(new System.Drawing.Point((int)cursorLogicalX, (int)cursorLogicalY));
                var bounds = screen.WorkingArea;

                // Clamp to working area
                if (left < bounds.Left) left = bounds.Left;
                if (top < bounds.Top) top = bounds.Top;
                if (left + windowWidth > bounds.Right) left = bounds.Right - windowWidth;
                if (top + windowHeight > bounds.Bottom) top = bounds.Bottom - windowHeight;
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
