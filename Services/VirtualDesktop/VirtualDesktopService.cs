using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Serilog;

namespace RadialLauncher.Services.VirtualDesktop
{
    public class DesktopInfo
    {
        public int Index { get; set; }
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsCurrent { get; set; }
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("a5cd92dd-dc7e-4161-b536-21a5e709ef06")]
    internal interface IVirtualDesktopManager
    {
        [PreserveSig]
        int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, out bool onCurrentDesktop);

        [PreserveSig]
        int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);

        [PreserveSig]
        int MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
    }

    [ComImport]
    [Guid("aa509085-0a56-466e-a726-c030d3222146")]
    internal class VirtualDesktopManagerCom
    {
    }

    public class VirtualDesktopService : IVirtualDesktopService
    {
        private readonly IVirtualDesktopManager? _manager;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_LWIN = 0x5B;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_LEFT = 0x25;
        private const byte VK_RIGHT = 0x27;
        private const byte VK_D = 0x44;

        public VirtualDesktopService()
        {
            try
            {
                _manager = (IVirtualDesktopManager)new VirtualDesktopManagerCom();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to initialize IVirtualDesktopManager COM object");
                _manager = null;
            }
        }

        public IReadOnlyList<DesktopInfo> GetDesktops()
        {
            var list = new List<DesktopInfo>();
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops");
                if (key != null)
                {
                    byte[]? currentBytes = key.GetValue("CurrentVirtualDesktop") as byte[];
                    Guid currentGuid = Guid.Empty;
                    if (currentBytes != null && currentBytes.Length == 16)
                    {
                        try
                        {
                            currentGuid = new Guid(currentBytes);
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, "Failed to parse CurrentVirtualDesktop GUID");
                        }
                    }

                    byte[]? idsBytes = key.GetValue("VirtualDesktopIDs") as byte[];
                    if (idsBytes != null && idsBytes.Length >= 16)
                    {
                        int count = idsBytes.Length / 16;
                        for (int i = 0; i < count; i++)
                        {
                            byte[] guidBytes = new byte[16];
                            Buffer.BlockCopy(idsBytes, i * 16, guidBytes, 0, 16);
                            try
                            {
                                var guid = new Guid(guidBytes);
                                if (guid != Guid.Empty)
                                {
                                    list.Add(new DesktopInfo
                                    {
                                        Index = list.Count,
                                        Id = guid,
                                        Name = $"Masaüstü {list.Count + 1}",
                                        IsCurrent = (currentGuid != Guid.Empty && guid == currentGuid)
                                    });
                                }
                            }
                            catch (Exception guidEx)
                            {
                                Log.Warning(guidEx, "Malformed GUID at virtual desktop index {Index}", i);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to enumerate virtual desktops from registry");
            }

            return list;
        }

        public bool MoveWindowToDesktop(IntPtr hWnd, int desktopIndex)
        {
            if (hWnd == IntPtr.Zero || desktopIndex < 0) return false;
            var desktops = GetDesktops();
            if (desktopIndex < desktops.Count)
            {
                return MoveWindowToDesktop(hWnd, desktops[desktopIndex].Id);
            }
            return false;
        }

        public bool MoveWindowToDesktop(IntPtr hWnd, Guid desktopId)
        {
            if (_manager == null || hWnd == IntPtr.Zero || desktopId == Guid.Empty) return false;
            try
            {
                int hr = _manager.MoveWindowToDesktop(hWnd, ref desktopId);
                if (hr == 0)
                {
                    Log.Information("Moved window {Hwnd} to desktop {DesktopId}", hWnd, desktopId);
                    return true;
                }
                Log.Warning("MoveWindowToDesktop returned HRESULT 0x{Hr:X}", hr);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to move window {Hwnd} to desktop {DesktopId}", hWnd, desktopId);
            }
            return false;
        }

        public void SwitchToDesktop(int targetIndex)
        {
            if (targetIndex < 0) return;
            var desktops = GetDesktops();
            if (desktops.Count == 0 || targetIndex >= desktops.Count) return;

            int currentIndex = 0;
            bool foundCurrent = false;
            for (int i = 0; i < desktops.Count; i++)
            {
                if (desktops[i].IsCurrent)
                {
                    currentIndex = i;
                    foundCurrent = true;
                    break;
                }
            }

            if (!foundCurrent) return;

            int diff = targetIndex - currentIndex;
            if (diff > 0)
            {
                for (int i = 0; i < diff; i++) SwitchToNextDesktop();
            }
            else if (diff < 0)
            {
                for (int i = 0; i < -diff; i++) SwitchToPreviousDesktop();
            }
        }

        public void SwitchToNextDesktop()
        {
            try
            {
                keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                keybd_event(VK_RIGHT, 0, 0, UIntPtr.Zero);
                keybd_event(VK_RIGHT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to switch to next virtual desktop");
            }
        }

        public void SwitchToPreviousDesktop()
        {
            try
            {
                keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                keybd_event(VK_LEFT, 0, 0, UIntPtr.Zero);
                keybd_event(VK_LEFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to switch to previous virtual desktop");
            }
        }

        public void CreateNewDesktop()
        {
            try
            {
                keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                keybd_event(VK_D, 0, 0, UIntPtr.Zero);
                keybd_event(VK_D, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to create new virtual desktop");
            }
        }
    }
}
