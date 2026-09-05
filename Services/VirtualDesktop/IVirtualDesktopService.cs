namespace RadialLauncher.Services.VirtualDesktop
{
    public interface IVirtualDesktopService
    {
        void SwitchToNextDesktop();
        void SwitchToPreviousDesktop();
        void CreateNewDesktop();
    }
}
