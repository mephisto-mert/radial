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

        public int CurrentVolumePercent => _currentVolumePercent;

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
            _currentVolumePercent = Math.Clamp(_currentVolumePercent + deltaPercent, 0, 100);
            if (deltaPercent != 0)
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
    }
}
