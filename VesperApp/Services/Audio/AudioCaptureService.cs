using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using PortAudioSharp;

namespace VesperApp.Services.Audio
{
    /// <summary>One capture device as PortAudio enumerates it (a physical device can
    /// appear once per host API on Windows: MME, DirectSound, WASAPI, WDM-KS).</summary>
    public sealed record AudioInputDevice(int Index, string Name, int MaxChannels, double DefaultSampleRate, int HostApi)
    {
        public override string ToString() => $"{Name}  ({MaxChannels} ch)";
    }

    /// <summary>
    /// Multi-channel sample history shared between the capture callback (writer) and
    /// the visualisers (readers): one float ring per channel, samples in [-1, 1].
    /// Readers copy the most recent N samples under a short lock; the writer never
    /// allocates.
    /// </summary>
    public sealed class LiveAudioBuffer
    {
        private readonly float[][] _ring;
        private readonly object _lock = new();
        private int _head;               // next write index
        private long _total;             // frames written since Reset

        public LiveAudioBuffer(int channels, int sampleRate, double seconds)
        {
            Channels = channels;
            SampleRate = sampleRate;
            Capacity = Math.Max(1024, (int)(sampleRate * seconds));
            _ring = new float[channels][];
            for (int c = 0; c < channels; c++) _ring[c] = new float[Capacity];
        }

        public int Channels { get; }
        public int SampleRate { get; }
        public int Capacity { get; }
        public long TotalFrames => Interlocked.Read(ref _total);

        /// <summary>Peak |sample| per channel in the last written block (for meters).</summary>
        public float[] BlockPeak { get; }  = new float[16];

        public void WriteInterleavedInt16(ReadOnlySpan<short> data, int frames)
        {
            lock (_lock)
            {
                int ch = Channels;
                for (int c = 0; c < ch && c < BlockPeak.Length; c++) BlockPeak[c] = 0f;
                int head = _head;
                for (int i = 0; i < frames; i++)
                {
                    for (int c = 0; c < ch; c++)
                    {
                        float v = data[i * ch + c] * (1f / 32768f);
                        _ring[c][head] = v;
                        float a = v < 0 ? -v : v;
                        if (c < BlockPeak.Length && a > BlockPeak[c]) BlockPeak[c] = a;
                    }
                    head++;
                    if (head == Capacity) head = 0;
                }
                _head = head;
                Interlocked.Add(ref _total, frames);
            }
        }

        public void WriteInterleavedFloat(ReadOnlySpan<float> data, int frames)
        {
            lock (_lock)
            {
                int ch = Channels;
                for (int c = 0; c < ch && c < BlockPeak.Length; c++) BlockPeak[c] = 0f;
                int head = _head;
                for (int i = 0; i < frames; i++)
                {
                    for (int c = 0; c < ch; c++)
                    {
                        float v = data[i * ch + c];
                        _ring[c][head] = v;
                        float a = v < 0 ? -v : v;
                        if (c < BlockPeak.Length && a > BlockPeak[c]) BlockPeak[c] = a;
                    }
                    head++;
                    if (head == Capacity) head = 0;
                }
                _head = head;
                Interlocked.Add(ref _total, frames);
            }
        }

        /// <summary>Copy the latest <c>dst.Length</c> samples of a channel (oldest first).
        /// Samples not yet written read as 0.</summary>
        public void CopyLatest(int channel, Span<float> dst)
        {
            if (channel < 0 || channel >= Channels) { dst.Clear(); return; }
            lock (_lock)
            {
                int n = Math.Min(dst.Length, Capacity);
                int start = _head - n; if (start < 0) start += Capacity;
                float[] src = _ring[channel];
                int first = Math.Min(n, Capacity - start);
                src.AsSpan(start, first).CopyTo(dst.Slice(dst.Length - n));
                if (first < n) src.AsSpan(0, n - first).CopyTo(dst.Slice(dst.Length - n + first));
                if (n < dst.Length) dst.Slice(0, dst.Length - n).Clear();
            }
        }

        /// <summary>Latest N samples averaged over all channels (mono mix).</summary>
        public void CopyLatestMix(Span<float> dst)
        {
            dst.Clear();
            float[] tmp = new float[dst.Length];
            for (int c = 0; c < Channels; c++)
            {
                CopyLatest(c, tmp);
                for (int i = 0; i < dst.Length; i++) dst[i] += tmp[i];
            }
            float g = 1f / Math.Max(1, Channels);
            for (int i = 0; i < dst.Length; i++) dst[i] *= g;
        }
    }

    /// <summary>
    /// Thin PortAudio capture wrapper for Live View: device enumeration (with rescan,
    /// because PortAudio snapshots devices at Initialize and the KOL appears only after
    /// its mode switch), one input stream, and a <see cref="LiveAudioBuffer"/> the
    /// visualisers read from. All PortAudio calls are serialised on a static lock.
    /// </summary>
    public sealed class AudioCaptureService : IDisposable
    {
        private static readonly object PaLock = new();
        private static bool _paReady;
        private static string? _paError;

        private PortAudioSharp.Stream? _stream;
        private short[] _i16 = new short[0];
        private float[] _f32 = new float[0];
        private PortAudioSharp.Stream.Callback? _cb;   // keep the delegate alive for the native side

        public LiveAudioBuffer? Buffer { get; private set; }
        /// <summary>Same stream after the optional local <see cref="Processor"/> (gain / filters); null when no processor is set.</summary>
        public LiveAudioBuffer? ProcessedBuffer { get; private set; }
        /// <summary>Local processing applied in the capture callback. Swap atomically; null = none.</summary>
        public volatile ProcessingChain? Processor;
        public AudioInputDevice? Device { get; private set; }
        public bool IsRunning => _stream != null && _stream.IsActive;
        public string? LastError { get; private set; }
        public long Overflows { get; private set; }

        /// <summary>Why PortAudio is unavailable on this machine, or null when it works.</summary>
        public static string? UnavailableReason
        {
            get { EnsureInit(); return _paError; }
        }

        private static void EnsureInit()
        {
            lock (PaLock)
            {
                if (_paReady || _paError != null) return;
                try
                {
                    PortAudio.LoadNativeLibrary();
                    PortAudio.Initialize();
                    _paReady = true;
                }
                catch (Exception ex)
                {
                    // A DllNotFoundException here usually means a dependency of the native
                    // library is missing rather than the library itself: on Windows
                    // portaudio.dll needs vcruntime140.dll (shipped next to VesperApp.exe),
                    // on Linux libportaudio.so needs the ALSA and JACK client libraries.
                    _paError = ex is DllNotFoundException
                        ? ex.Message + (OperatingSystem.IsWindows()
                            ? " Check that portaudio.dll and vcruntime140.dll are next to VesperApp.exe."
                            : " Check that libportaudio.so is next to VesperApp and that libasound2 and libjack are installed.")
                        : ex.Message;
                }
            }
        }

        /// <summary>Re-enumerate devices (PortAudio only sees devices present at Initialize).
        /// Must not be called while any stream is open.</summary>
        public static void Rescan()
        {
            lock (PaLock)
            {
                if (!_paReady) { EnsureInit(); return; }
                try { PortAudio.Terminate(); PortAudio.Initialize(); }
                catch (Exception ex) { _paError = ex.Message; _paReady = false; }
            }
        }

        public static IReadOnlyList<AudioInputDevice> ListInputs()
        {
            EnsureInit();
            var list = new List<AudioInputDevice>();
            lock (PaLock)
            {
                if (!_paReady) return list;
                int n;
                try { n = PortAudio.DeviceCount; } catch { return list; }
                for (int i = 0; i < n; i++)
                {
                    try
                    {
                        DeviceInfo d = PortAudio.GetDeviceInfo(i);
                        if (d.maxInputChannels > 0)
                            list.Add(new AudioInputDevice(i, d.name ?? $"device {i}", d.maxInputChannels, d.defaultSampleRate, d.hostApi));
                    }
                    catch { /* skip broken entries */ }
                }
            }
            return list;
        }

        /// <summary>The KOL's USB microphone entries (4-channel UAC2), best candidate first:
        /// the entry whose default rate matches, then the highest host API index (on
        /// Windows that is WDM-KS, the only path that exposes all 4 channels reliably).</summary>
        public static IReadOnlyList<AudioInputDevice> FindKolCandidates(int wantRate)
        {
            return ListInputs()
                .Where(d => d.Name.Contains("KOL", StringComparison.OrdinalIgnoreCase) && d.MaxChannels >= 4)
                .OrderByDescending(d => Math.Abs(d.DefaultSampleRate - wantRate) < 1 ? 1 : 0)
                .ThenByDescending(d => d.HostApi)
                .ToList();
        }

        /// <summary>Open and start a capture. Returns false (with <see cref="LastError"/>) on failure.</summary>
        public bool Start(AudioInputDevice device, int channels, int sampleRate, double historySeconds = 12, uint framesPerBuffer = 480)
        {
            Stop();
            EnsureInit();
            if (!_paReady) { LastError = _paError ?? "PortAudio not available"; return false; }

            channels = Math.Clamp(channels, 1, device.MaxChannels);
            var buf = new LiveAudioBuffer(channels, sampleRate, historySeconds);
            var pbuf = new LiveAudioBuffer(channels, sampleRate, historySeconds);
            var p = new StreamParameters
            {
                device = device.Index,
                channelCount = channels,
                sampleFormat = SampleFormat.Int16,
                suggestedLatency = 0.03,
                hostApiSpecificStreamInfo = IntPtr.Zero
            };
            _cb = (IntPtr input, IntPtr output, uint frameCount, ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags flags, IntPtr userData) =>
            {
                try
                {
                    if ((flags & StreamCallbackFlags.InputOverflow) != 0) Overflows++;
                    int frames = (int)frameCount;
                    int need = frames * channels;
                    if (_i16.Length < need) _i16 = new short[need];
                    if (input != IntPtr.Zero)
                    {
                        Marshal.Copy(input, _i16, 0, need);
                        buf.WriteInterleavedInt16(_i16, frames);
                        ProcessingChain? proc = Processor;
                        if (proc != null && proc.Channels == channels)
                        {
                            if (_f32.Length < need) _f32 = new float[need];
                            proc.Process(_i16, _f32, frames);
                            pbuf.WriteInterleavedFloat(_f32, frames);
                        }
                    }
                }
                catch { /* never throw into native code */ }
                return StreamCallbackResult.Continue;
            };
            lock (PaLock)
            {
                try
                {
                    _stream = new PortAudioSharp.Stream(p, null, sampleRate, framesPerBuffer, StreamFlags.ClipOff, _cb, IntPtr.Zero);
                    _stream.Start();
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    try { _stream?.Dispose(); } catch { }
                    _stream = null;
                    return false;
                }
            }
            Buffer = buf;
            ProcessedBuffer = pbuf;
            Device = device with { MaxChannels = channels };
            LastError = null;
            return true;
        }

        public void Stop()
        {
            lock (PaLock)
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
