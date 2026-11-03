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
