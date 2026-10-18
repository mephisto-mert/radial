using System;
using System.IO;
using System.Media;
using System.Text;
using System.Threading.Tasks;

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

    public sealed class UiSoundEffectsService : IUiSoundEffectsService
    {
        private const int SampleRate = 22050;

        private readonly byte[] _hoverTickWav;
        private readonly byte[] _bloomOpenWav;
        private readonly byte[] _submenuDiveWav;
        private readonly byte[] _launchConfirmWav;
        private readonly byte[] _gamingBoostWav;

        public bool IsEnabled { get; set; } = true;

        public UiSoundEffectsService()
        {
            _hoverTickWav = SynthesizeSweepWav(durationMs: 7, startFreqHz: 1350.0, endFreqHz: 850.0, amplitude: 0.12);
            _bloomOpenWav = SynthesizeSweepWav(durationMs: 40, startFreqHz: 420.0, endFreqHz: 880.0, amplitude: 0.16, harmonicMix: 0.25);
            _submenuDiveWav = SynthesizeDualToneStepWav(durationMs: 35, firstFreqHz: 660.0, secondFreqHz: 990.0, amplitude: 0.16);
            _launchConfirmWav = SynthesizeChordWav(durationMs: 50, freqAHz: 523.0, freqBHz: 784.0, amplitude: 0.18);
            _gamingBoostWav = SynthesizeSweepWav(durationMs: 70, startFreqHz: 440.0, endFreqHz: 1100.0, amplitude: 0.20, harmonicMix: 0.30);
        }

        public void PlayHoverTick() => PlayWavAsync(_hoverTickWav);

        public void PlayBloomOpen() => PlayWavAsync(_bloomOpenWav);

        public void PlaySubmenuDive() => PlayWavAsync(_submenuDiveWav);

        public void PlayLaunchConfirm() => PlayWavAsync(_launchConfirmWav);

        public void PlayGamingBoost() => PlayWavAsync(_gamingBoostWav);

        private void PlayWavAsync(byte[] wavBytes)
        {
            if (!IsEnabled || wavBytes.Length == 0)
            {
                return;
            }

            _ = Task.Run(() =>
            {
                try
                {
                    using var stream = new MemoryStream(wavBytes, writable: false);
                    using var player = new SoundPlayer(stream);
                    player.PlaySync();
                }
                catch
                {
                    // Ignore audio device contention errors
                }
            });
        }

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
                {
                    signal = (signal + harmonicMix * Math.Sin(phase * 2.0)) / (1.0 + harmonicMix);
                }

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

                double signal = Math.Sin(phase);
                samples[i] = (short)Math.Clamp(signal * envelope * amplitude * short.MaxValue, short.MinValue, short.MaxValue);
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

            for (int i = 0; i < samples.Length; i++)
            {
                writer.Write(samples[i]);
            }

            writer.Flush();
            return ms.ToArray();
        }
    }
}
