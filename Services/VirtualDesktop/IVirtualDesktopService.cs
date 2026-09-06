using System;
using System.Collections.Generic;

namespace RadialLauncher.Services.VirtualDesktop
{
    public interface IVirtualDesktopService
    {
        IReadOnlyList<DesktopInfo> GetDesktops();
        bool MoveWindowToDesktop(IntPtr hWnd, int desktopIndex);
        bool MoveWindowToDesktop(IntPtr hWnd, Guid desktopId);
        void SwitchToDesktop(int desktopIndex);
        void SwitchToNextDesktop();
        void SwitchToPreviousDesktop();
        void CreateNewDesktop();
    }
}
