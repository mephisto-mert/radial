using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace RadialLauncher.Services.Audio
{
    public interface IUiSoundEffectsService
    {
        bool IsEnabled { get; set; }
        void PlayHoverTick();
        void PlayBloomOpen();
        void PlaySubmenuDive();
        void PlayLaunchConfirm();
        void PlayGamingBoost();
    }

    public sealed class UiSoundEffectsService : IUiSoundEffectsService, IDisposable
    {
        private const int SampleRate = 44100;
        private const uint SND_ASYNC = 0x0001;
        private const uint SND_NODEFAULT = 0x0002;
        private const uint SND_MEMORY = 0x0004;

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern bool PlaySound(IntPtr pszSound, IntPtr hmod, uint fdwSound);

        private readonly GCHandle _silentWarmupPin;
        private readonly GCHandle _hoverTickPin;
        private readonly GCHandle _bloomOpenPin;
        private readonly GCHandle _submenuDivePin;
        private readonly GCHandle _launchConfirmPin;
        private readonly GCHandle _gamingBoostPin;

        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _lastHoverTickMs = -1000;
        private long _prioritySoundProtectedUntilMs = -1000;
        private bool _disposed;

        public bool IsEnabled { get; set; } = true;

        public UiSoundEffectsService()
        {
            _silentWarmupPin = PinWav(SynthesizeSweepWav(durationMs: 4, startFreqHz: 440.0, endFreqHz: 440.0, amplitude: 0.0));
            _hoverTickPin = PinWav(SynthesizeSweepWav(durationMs: 6, startFreqHz: 1450.0, endFreqHz: 920.0, amplitude: 0.11));
            _bloomOpenPin = PinWav(SynthesizeSweepWav(durationMs: 28, startFreqHz: 440.0, endFreqHz: 880.0, amplitude: 0.15, harmonicMix: 0.22));
            _submenuDivePin = PinWav(SynthesizeDualToneStepWav(durationMs: 26, firstFreqHz: 660.0, secondFreqHz: 990.0, amplitude: 0.15));
            _launchConfirmPin = PinWav(SynthesizeChordWav(durationMs: 36, freqAHz: 523.25, freqBHz: 783.99, amplitude: 0.17));
            _gamingBoostPin = PinWav(SynthesizeSweepWav(durationMs: 55, startFreqHz: 440.0, endFreqHz: 1120.0, amplitude: 0.19, harmonicMix: 0.25));

            // Warm up WinMM audio pipeline asynchronously with 0-amplitude buffer so first sound has 0ms cold-start latency
            try
            {
                PlaySound(_silentWarmupPin.AddrOfPinnedObject(), IntPtr.Zero, SND_MEMORY | SND_ASYNC | SND_NODEFAULT);
            }
            catch { }
        }

        public void PlayHoverTick()
        {
            if (!IsEnabled || _disposed) return;
            long now = _clock.ElapsedMilliseconds;
            // Do not let an immediate hover tick overwrite a bloom/launch/dive sound, and throttle rapid movement to 22ms
            if (now < _prioritySoundProtectedUntilMs || (now - _lastHoverTickMs) < 22)
            {
                return;
            }
            _lastHoverTickMs = now;
            PlayPinned(_hoverTickPin);
        }

        public void PlayBloomOpen() => PlayPriority(_bloomOpenPin, protectionMs: 40);

        public void PlaySubmenuDive() => PlayPriority(_submenuDivePin, protectionMs: 38);

        public void PlayLaunchConfirm() => PlayPriority(_launchConfirmPin, protectionMs: 50);

        public void PlayGamingBoost() => PlayPriority(_gamingBoostPin, protectionMs: 65);

        private void PlayPriority(GCHandle pin, int protectionMs)
        {
            if (!IsEnabled || _disposed) return;
            _prioritySoundProtectedUntilMs = _clock.ElapsedMilliseconds + protectionMs;
            PlayPinned(pin);
        }

        private static void PlayPinned(GCHandle pin)
        {
            if (!pin.IsAllocated) return;
            try
            {
                PlaySound(pin.AddrOfPinnedObject(), IntPtr.Zero, SND_MEMORY | SND_ASYNC | SND_NODEFAULT);
            }
            catch { }
        }

        private static GCHandle PinWav(byte[] wavBytes) => GCHandle.Alloc(wavBytes, GCHandleType.Pinned);

        private static byte[] SynthesizeSweepWav(int durationMs, double startFreqHz, double endFreqHz, double amplitude, double harmonicMix = 0.0)
        {
            int sampleCount = Math.Max(1, (SampleRate * durationMs) / 1000);
            short[] samples = new short[sampleCount];
            double phase = 0.0;
            for (int i = 0; i < sampleCount; i++)
            {
                double progress = (double)i / sampleCount;
                double envelope = Math.Sin(Math.PI * progress);
                double freq = startFreqHz + (endFreqHz - startFreqHz) * progress;
                phase += 2.0 * Math.PI * freq / SampleRate;
                double signal = Math.Sin(phase);
                if (harmonicMix > 0.0)
                    signal = (signal + harmonicMix * Math.Sin(phase * 2.0)) / (1.0 + harmonicMix);
                samples[i] = (short)Math.Clamp(signal * envelope * amplitude * short.MaxValue, short.MinValue, short.MaxValue);
            }
            return EncodePcm16Wav(samples);
        }

        private static byte[] SynthesizeDualToneStepWav(int durationMs, double firstFreqHz, double secondFreqHz, double amplitude)
        {
            int sampleCount = Math.Max(1, (SampleRate * durationMs) / 1000);
            short[] samples = new short[sampleCount];
            double phase = 0.0;
            for (int i = 0; i < sampleCount; i++)
            {
                double progress = (double)i / sampleCount;
                double envelope = Math.Sin(Math.PI * progress);
                double freq = progress < 0.5 ? firstFreqHz : secondFreqHz;
                phase += 2.0 * Math.PI * freq / SampleRate;
                samples[i] = (short)Math.Clamp(Math.Sin(phase) * envelope * amplitude * short.MaxValue, short.MinValue, short.MaxValue);
            }
            return EncodePcm16Wav(samples);
        }

        private static byte[] SynthesizeChordWav(int durationMs, double freqAHz, double freqBHz, double amplitude)
        {
            int sampleCount = Math.Max(1, (SampleRate * durationMs) / 1000);
            short[] samples = new short[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                double progress = (double)i / sampleCount;
                double envelope = Math.Sin(Math.PI * progress);
                double t = (double)i / SampleRate;
                double signal = 0.5 * (Math.Sin(2.0 * Math.PI * freqAHz * t) + Math.Sin(2.0 * Math.PI * freqBHz * t));
                samples[i] = (short)Math.Clamp(signal * envelope * amplitude * short.MaxValue, short.MinValue, short.MaxValue);
            }
            return EncodePcm16Wav(samples);
        }

        private static byte[] EncodePcm16Wav(short[] samples)
        {
            int dataBytes = samples.Length * sizeof(short);
            using var ms = new MemoryStream(44 + dataBytes);
            using var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataBytes);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(SampleRate);
            writer.Write(SampleRate * sizeof(short));
            writer.Write((short)sizeof(short));
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataBytes);
            for (int i = 0; i < samples.Length; i++) writer.Write(samples[i]);
            writer.Flush();
            return ms.ToArray();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_silentWarmupPin.IsAllocated) _silentWarmupPin.Free();
            if (_hoverTickPin.IsAllocated) _hoverTickPin.Free();
            if (_bloomOpenPin.IsAllocated) _bloomOpenPin.Free();
            if (_submenuDivePin.IsAllocated) _submenuDivePin.Free();
            if (_launchConfirmPin.IsAllocated) _launchConfirmPin.Free();
            if (_gamingBoostPin.IsAllocated) _gamingBoostPin.Free();
        }
    }
}
