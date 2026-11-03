using System;
using System.Runtime.InteropServices;
using PortAudioSharp;

namespace VesperApp.Services.Audio
{
    /// <summary>
    /// Sum of steady sine tones with a short fade-in, for the microphone check's stimulus
    /// (<c>Specs/audio-selftest-spec.json</c>: 1 kHz + 4 kHz). Pure, so it is unit-tested.
    /// </summary>
    public sealed class ToneGenerator
    {
        private readonly double[] _step;
        private readonly double[] _phase;
        private readonly float _amplitude;
        private readonly long _fadeSamples;
        private long _n;

        /// <param name="amplitude">Peak of each tone (full scale 1.0); the sum peaks at tones × amplitude.</param>
        public ToneGenerator(int sampleRate, int[] tonesHz, float amplitude, double fadeSeconds = 0.02)
        {
            _step = Array.ConvertAll(tonesHz, f => 2 * Math.PI * f / sampleRate);
            _phase = new double[tonesHz.Length];
            _amplitude = amplitude;
            _fadeSamples = Math.Max(1, (long)(fadeSeconds * sampleRate));
        }

        /// <summary>Writes <paramref name="frames"/> frames of <paramref name="channels"/> interleaved channels (the
        /// same signal in each).</summary>
        public void Fill(float[] dst, int frames, int channels)
        {
            for (int i = 0; i < frames; i++, _n++)
            {
                double s = 0;
                for (int t = 0; t < _step.Length; t++)
                {
                    s += Math.Sin(_phase[t]);
                    _phase[t] += _step[t];
                    if (_phase[t] > 2 * Math.PI) _phase[t] -= 2 * Math.PI;
                }
                float v = (float)(s * _amplitude * Math.Min(1.0, (double)_n / _fadeSamples));
                for (int c = 0; c < channels; c++) dst[i * channels + c] = v;
            }
        }
    }

    /// <summary>
    /// Whether test tones are in a recording: each tone's level (Goertzel, Hann window) against the median
    /// level a little below and above it (±8…20 %), in dB. A steady room noise or hum raises both alike;
    /// only a played tone stands out. Pure, unit-tested.
    /// </summary>
    public static class ToneDetector
    {
        public const double HeardMinDb = 12;

        public static double[] SnrDb(ReadOnlySpan<float> x, int sampleRate, int[] tonesHz)
        {
            var r = new double[tonesHz.Length];
            for (int t = 0; t < tonesHz.Length; t++)
            {
                double f = tonesHz[t];
                double tone = Level(x, sampleRate, f);
                var side = new System.Collections.Generic.List<double>();
                foreach (double k in new[] { 0.08, 0.12, 0.16, 0.20 })
                {
                    side.Add(Level(x, sampleRate, f * (1 - k)));
                    side.Add(Level(x, sampleRate, f * (1 + k)));
                }
                side.Sort();
                double floor = Math.Max(1e-9, (side[3] + side[4]) / 2);
                r[t] = 20 * Math.Log10(Math.Max(1e-12, tone) / floor);
            }
            return r;
        }

        public static bool Heard(double[] snrDb) => snrDb.Length > 0 && Array.TrueForAll(snrDb, s => s >= HeardMinDb);

        /// <summary>A microphone that records digital silence (below −90 dBFS RMS) is muted or switched off
        /// (bench 2026-11-03: a ThinkPad array with its mute on read −97 / −125 dBFS on every path); a live
        /// one never gets that quiet.</summary>
        public static bool IsSilent(ReadOnlySpan<float> x)
        {
            double sum = 0;
            foreach (float v in x) sum += (double)v * v;
            return x.Length == 0 || Math.Sqrt(sum / x.Length) < 3.16e-5;   // −90 dBFS
        }

        private static double Level(ReadOnlySpan<float> x, int rate, double f)
        {
            int n = x.Length;
            double w = 2 * Math.PI * f / rate, c = 2 * Math.Cos(w), s1 = 0, s2 = 0;
            for (int i = 0; i < n; i++)
            {
                double h = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (n - 1));
                double s0 = x[i] * h + c * s1 - s2; s2 = s1; s1 = s0;
            }
            return Math.Sqrt(Math.Max(0, s1 * s1 + s2 * s2 - c * s1 * s2));
        }
    }

    /// <summary>
    /// Plays the microphone check's test tones on this computer's default output (PortAudio, shared with
    /// <see cref="AudioCaptureService"/>), so the check needs no phone or tone app: without a tone the
    /// unit's microphone is healthy but always judged "Weak" (no tone SNR).
    /// </summary>
    public sealed class TonePlayer : IDisposable
    {
        private PortAudioSharp.Stream? _stream;
        private PortAudioSharp.Stream.Callback? _cb;   // keep the delegate alive for the native side
        private float[] _buf = new float[0];

        public string? LastError { get; private set; }
        public string? DeviceName { get; private set; }

        public bool Start(int[] tonesHz, float amplitude = 0.35f, int sampleRate = 48000)
        {
            Stop();
            if (!AudioCaptureService.TryInit(out string? err)) { LastError = err ?? "PortAudio not available"; return false; }
            lock (AudioCaptureService.PaLock)
            {
                try
                {
                    int dev = PortAudio.DefaultOutputDevice;
                    if (dev < 0) { LastError = "This computer has no audio output."; return false; }
                    DeviceInfo info = PortAudio.GetDeviceInfo(dev);
                    int channels = Math.Clamp(info.maxOutputChannels, 1, 2);
                    var gen = new ToneGenerator(sampleRate, tonesHz, amplitude);
                    var p = new StreamParameters
                    {
                        device = dev,
                        channelCount = channels,
                        sampleFormat = SampleFormat.Float32,
                        suggestedLatency = info.defaultHighOutputLatency,
                        hostApiSpecificStreamInfo = IntPtr.Zero
                    };
                    _cb = (IntPtr input, IntPtr output, uint frameCount, ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags flags, IntPtr userData) =>
                    {
                        try
                        {
                            int need = (int)frameCount * channels;
                            if (_buf.Length < need) _buf = new float[need];
                            gen.Fill(_buf, (int)frameCount, channels);
                            Marshal.Copy(_buf, 0, output, need);
                        }
                        catch { /* never throw into native code */ }
                        return StreamCallbackResult.Continue;
                    };
                    _stream = new PortAudioSharp.Stream(null, p, sampleRate, 0, StreamFlags.ClipOff, _cb, IntPtr.Zero);
                    _stream.Start();
                    DeviceName = info.name;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    try { _stream?.Dispose(); } catch { }
                    _stream = null;
                    return false;
                }
            }
            LastError = null;
            return true;
        }

        /// <summary>Result of <see cref="CheckHeard"/>: the tones' SNR on this PC's microphone, or null when
        /// the PC has no microphone of its own to listen with.</summary>
        public sealed record HeardResult(bool Heard, double[]? SnrDb, string? Microphone, bool MicSilent = false);

        /// <summary>While the tones play: listen on this PC's default microphone for half a second and tell
        /// whether they are really coming out (no speaker, muted, volume at 0 → not heard). A unit's own Live
        /// Audio microphone is not the PC's, so it does not count.</summary>
        public static HeardResult CheckHeard(int[] tonesHz)
        {
            if (!AudioCaptureService.TryInit(out _)) return new HeardResult(false, null, null);
            AudioInputDevice? mic = null;
            lock (AudioCaptureService.PaLock)
            {
                try
                {
                    int i = PortAudio.DefaultInputDevice;
                    if (i >= 0)
                    {
                        DeviceInfo d = PortAudio.GetDeviceInfo(i);
                        if (d.maxInputChannels > 0)
                            mic = new AudioInputDevice(i, d.name ?? $"device {i}", d.maxInputChannels, d.defaultSampleRate, d.hostApi);
                    }
                }
                catch { mic = null; }
            }
            if (mic == null || AudioCaptureService.IsUnitMicrophone(mic)) return new HeardResult(false, null, null);

            int sampleRate = mic.DefaultSampleRate >= 16000 ? (int)mic.DefaultSampleRate : 48000;   // the PC mic's own rate
            using var cap = new AudioCaptureService();
            if (!cap.Start(mic, 1, sampleRate, historySeconds: 2)) return new HeardResult(false, null, null);
            System.Threading.Thread.Sleep(700);
            var x = new float[sampleRate / 2];
            cap.Buffer!.CopyLatest(0, x);
            cap.Stop();
            double[] snr = ToneDetector.SnrDb(x, sampleRate, tonesHz);
            return new HeardResult(ToneDetector.Heard(snr), snr, mic.Name, ToneDetector.IsSilent(x));
        }

        public void Stop()
        {
            lock (AudioCaptureService.PaLock)
            {
                if (_stream == null) return;
                try { if (!_stream.IsStopped) _stream.Stop(); } catch { }
                try { _stream.Close(); } catch { }
                try { _stream.Dispose(); } catch { }
                _stream = null;
            }
            _cb = null;
        }

        public void Dispose() => Stop();
    }
}
