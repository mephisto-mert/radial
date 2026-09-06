using System;
using System.Collections.Generic;

namespace RadialLauncher.Services.Windows
{
    public interface IWindowSwitcherService
    {
        List<WindowInfo> GetOpenWindows();
        void SwitchToWindow(IntPtr hWnd);
        void CloseWindow(IntPtr hWnd);
        string GetForegroundProcessName();
    }
}
