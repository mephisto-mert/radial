using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RadialLauncher.Services.Audio
{
    public sealed record NowPlayingInfo(bool HasMedia, string Title, string Artist, string SourceApp)
    {
        public NowPlayingInfo() : this(false, "Medya Hazır", string.Empty, string.Empty)
        {
        }
    }

    public interface INowPlayingAndAudioService
    {
        NowPlayingInfo GetCurrentMediaInfo();
        int CurrentVolumePercent { get; }
        int AdjustMasterVolume(int deltaPercent);
        void PlayPause();
        void NextTrack();
        void PreviousTrack();
    }

    public sealed class NowPlayingAndAudioService : INowPlayingAndAudioService
    {
        private const byte VK_VOLUME_DOWN = 0xAE;
        private const byte VK_VOLUME_UP = 0xAF;
        private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
        private const byte VK_MEDIA_PREV_TRACK = 0xB1;
        private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private static readonly string[] ScanProcesses =
        {
            "Spotify", "vlc", "musicbee", "aimp", "chrome", "brave", "msedge"
        };

        private int _currentVolumePercent = 70;

        public int CurrentVolumePercent
        {
            get
            {
                if (TryGetCoreAudioVolumePercent(out int realVol))
                {
                    _currentVolumePercent = realVol;
                }
                return _currentVolumePercent;
            }
        }

        public NowPlayingInfo GetCurrentMediaInfo()
        {
            foreach (var processName in ScanProcesses)
            {
                try
                {
                    var processes = Process.GetProcessesByName(processName);
                    foreach (var proc in processes)
                    {
                        using (proc)
                        {
                            var title = proc.MainWindowTitle?.Trim();
                            if (string.IsNullOrWhiteSpace(title))
                            {
                                continue;
                            }

                            var parsed = TryParseWindowTitle(processName, title);
                            if (parsed.HasMedia)
                            {
                                return parsed;
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore inaccessible processes
                }
            }

            return new NowPlayingInfo(false, "Medya Hazır", string.Empty, string.Empty);
        }

        public int AdjustMasterVolume(int deltaPercent)
        {
            int baseVol = CurrentVolumePercent;
            int targetVol = Math.Clamp(baseVol + deltaPercent, 0, 100);
            _currentVolumePercent = targetVol;

            if (!TrySetCoreAudioVolumePercent(targetVol) && deltaPercent != 0)
            {
                byte vk = deltaPercent > 0 ? VK_VOLUME_UP : VK_VOLUME_DOWN;
                int steps = Math.Clamp(Math.Abs(deltaPercent) / 2, 1, 10);
                for (int i = 0; i < steps; i++)
                {
                    SendMediaKey(vk);
                }
            }

            return _currentVolumePercent;
        }

        private static bool TryGetCoreAudioVolumePercent(out int volumePercent)
        {
            volumePercent = 70;
            try
            {
                var endpointVol = GetEndpointVolumeInterface();
                if (endpointVol != null && endpointVol.GetMasterVolumeLevelScalar(out float level) == 0)
                {
                    volumePercent = Math.Clamp((int)Math.Round(level * 100.0f), 0, 100);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static bool TrySetCoreAudioVolumePercent(int volumePercent)
        {
            try
            {
                var endpointVol = GetEndpointVolumeInterface();
                if (endpointVol != null)
                {
                    float scalar = Math.Clamp(volumePercent / 100.0f, 0.0f, 1.0f);
                    Guid ctx = Guid.Empty;
                    if (volumePercent > 0)
                    {
                        endpointVol.SetMute(false, ref ctx);
                    }
                    return endpointVol.SetMasterVolumeLevelScalar(scalar, ref ctx) == 0;
                }
            }
            catch { }
            return false;
        }

        private static IAudioEndpointVolume? GetEndpointVolumeInterface()
        {
            var enumeratorType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
            if (enumeratorType == null) return null;
            var enumerator = (IMMDeviceEnumerator?)Activator.CreateInstance(enumeratorType);
            if (enumerator == null) return null;
            if (enumerator.GetDefaultAudioEndpoint(0, 1, out IMMDevice device) != 0 || device == null)
                return null;
            Guid iid = typeof(IAudioEndpointVolume).GUID;
            if (device.Activate(ref iid, 23, IntPtr.Zero, out object volObj) != 0)
                return null;
            return volObj as IAudioEndpointVolume;
        }

        public void PlayPause() => SendMediaKey(VK_MEDIA_PLAY_PAUSE);

        public void NextTrack() => SendMediaKey(VK_MEDIA_NEXT_TRACK);

        public void PreviousTrack() => SendMediaKey(VK_MEDIA_PREV_TRACK);

        private static NowPlayingInfo TryParseWindowTitle(string processName, string windowTitle)
        {
            if (processName.Equals("Spotify", StringComparison.OrdinalIgnoreCase) ||
                processName.Equals("vlc", StringComparison.OrdinalIgnoreCase) ||
                processName.Equals("musicbee", StringComparison.OrdinalIgnoreCase) ||
                processName.Equals("aimp", StringComparison.OrdinalIgnoreCase))
            {
                var cleaned = windowTitle
                    .Replace(" - VLC media player", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Trim();

                int sepIndex = cleaned.IndexOf(" - ", StringComparison.Ordinal);
                if (sepIndex > 0 && sepIndex + 3 < cleaned.Length)
                {
                    string artist = cleaned[..sepIndex].Trim();
                    string track = cleaned[(sepIndex + 3)..].Trim();
                    return new NowPlayingInfo(true, track, artist, processName);
                }
            }
            else
            {
                int ytMusicIdx = windowTitle.IndexOf(" - YouTube Music", StringComparison.OrdinalIgnoreCase);
                if (ytMusicIdx > 0)
                {
                    string track = windowTitle[..ytMusicIdx].Trim();
                    return new NowPlayingInfo(true, track, "YouTube Music", processName);
                }

                int ytIdx = windowTitle.IndexOf(" - YouTube", StringComparison.OrdinalIgnoreCase);
                if (ytIdx > 0)
                {
                    string track = windowTitle[..ytIdx].Trim();
                    return new NowPlayingInfo(true, track, "YouTube", processName);
                }
            }

            return new NowPlayingInfo(false, "Medya Hazır", string.Empty, string.Empty);
        }

        private static void SendMediaKey(byte virtualKey)
        {
            try
            {
                keybd_event(virtualKey, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
                keybd_event(virtualKey, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch
            {
                // Ignore Win32 input dispatch errors
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig]
            int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
            [PreserveSig]
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        }

        [ComImport]
        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            [PreserveSig] int RegisterControlChangeNotify(IntPtr pNotify);
            [PreserveSig] int UnregisterControlChangeNotify(IntPtr pNotify);
            [PreserveSig] int GetChannelCount(out uint pnChannelCount);
            [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
            [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
            [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
            [PreserveSig] int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
            [PreserveSig] int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
            [PreserveSig] int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
            [PreserveSig] int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
            [PreserveSig] int GetMute(out bool pbMute);
        }
    }
}
