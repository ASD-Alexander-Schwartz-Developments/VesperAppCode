using System;
using Avalonia.Media;
using ReactiveUI;

namespace VesperApp.Models
{
    /// <summary>
    /// Microphone bench-test (VESPER_TEST_AUDIO) request/response packing — the
    /// same wire format the device firmware and the factory test suite use.
    ///
    /// Request (53 bytes, little-endian, packed):
    ///   uint32 sampleRate; uint16 durationMs; uint8 snrThreshDb; uint8 nTones;
    ///   uint32 freqHz[11];  uint8 micIndex;
    /// Response payload (header/checksum already stripped by SerialMessage):
    ///   [passMask u16][noiseFloorDb u16][nTones u8][magCounts u16*n][snrDb u8*n]
    /// then, from the shared firmware engine (KOL &gt;= 1.8, Vesper / Pipistrelle feature release;
    /// audio_selftest.h), a 15-byte trailer at t = 5 + 3n:
    ///   [t] format = 1 [t+1] mic index [t+2] status (AudioSelftestStatus) [t+3] i16 level without DC,
    ///   dBFS x 100 (-32768 none) [t+5] i16 DC [t+7] u16 peak [t+9] u16 clipped samples [t+11] u32 rate, Hz
    /// Older firmware sends the v0 part only; <see cref="AudioBenchResult.HasTrailer"/> tells them apart.
    /// </summary>
    public static class AudioBenchTest
    {
        public const int MaxTones = 11;

        public static byte[] BuildRequest(uint sampleRate, ushort durationMs, byte snrThreshDb,
                                          int[] tones, byte micIndex)
        {
            int n = tones?.Length ?? 0;
            if (n < 1 || n > MaxTones)
                throw new ArgumentException($"need 1..{MaxTones} tones", nameof(tones));

            byte[] buf = new byte[4 + 2 + 1 + 1 + (4 * MaxTones) + 1];   // 53
            int o = 0;
            buf[o++] = (byte)(sampleRate & 0xFF);
            buf[o++] = (byte)((sampleRate >> 8) & 0xFF);
            buf[o++] = (byte)((sampleRate >> 16) & 0xFF);
            buf[o++] = (byte)((sampleRate >> 24) & 0xFF);
            buf[o++] = (byte)(durationMs & 0xFF);
            buf[o++] = (byte)((durationMs >> 8) & 0xFF);
            buf[o++] = snrThreshDb;
            buf[o++] = (byte)n;
            for (int i = 0; i < MaxTones; i++)
            {
                uint f = (i < n) ? (uint)tones![i] : 0u;
                buf[o++] = (byte)(f & 0xFF);
                buf[o++] = (byte)((f >> 8) & 0xFF);
                buf[o++] = (byte)((f >> 16) & 0xFF);
                buf[o++] = (byte)((f >> 24) & 0xFF);
            }
            buf[o++] = micIndex;
            return buf;
        }

        public static AudioBenchResult? ParseResponse(byte[]? payload)
        {
            if (payload == null || payload.Length < 5)
                return null;

            int o = 0;
            ushort passMask = (ushort)(payload[o++] | (payload[o++] << 8));
            ushort noiseFloorDb = (ushort)(payload[o++] | (payload[o++] << 8));
            int n = payload[o++];

            if (n < 0 || n > MaxTones || payload.Length < 5 + (2 * n) + n)
                return null;

            int[] mags = new int[n];
            for (int i = 0; i < n; i++) mags[i] = payload[o++] | (payload[o++] << 8);
            int[] snr = new int[n];
            for (int i = 0; i < n; i++) snr[i] = payload[o++];

            bool[] perTone = new bool[n];
            for (int i = 0; i < n; i++) perTone[i] = ((passMask >> i) & 1) != 0;

            var r = new AudioBenchResult
            {
                PassMask = passMask,
                NoiseFloorDb = noiseFloorDb,
                NTones = n,
                MagCounts = mags,
                SnrDb = snr,
                PerTonePass = perTone,
            };

            int t = 5 + 3 * n;
            if (payload.Length >= t + TrailerLength && payload[t] == TrailerFormat)
            {
                r.HasTrailer = true;
                r.MicIndex = payload[t + 1];
                r.Status = (AudioSelftestStatus)payload[t + 2];
                short level = (short)(payload[t + 3] | (payload[t + 4] << 8));
                r.LevelDbfs = level == short.MinValue ? null : level / 100.0;
                r.Dc = (short)(payload[t + 5] | (payload[t + 6] << 8));
                r.Peak = payload[t + 7] | (payload[t + 8] << 8);
                r.ClippedSamples = payload[t + 9] | (payload[t + 10] << 8);
                r.SampleRateHz = (uint)(payload[t + 11] | (payload[t + 12] << 8) | (payload[t + 13] << 16) | (payload[t + 14] << 24));
            }
            return r;
        }

        public const int TrailerLength = 15;
        public const byte TrailerFormat = 1;
    }

    /// <summary>Status bits of the self-test trailer (firmware audio_selftest.h AST_ST_*).</summary>
    [Flags]
    public enum AudioSelftestStatus : byte
    {
        None = 0,
        Captured = 0x01,
        Clipped = 0x02,
        Stuck = 0x04,
        Inexact = 0x08,
        NoMic = 0x10,
        Busy = 0x20,
        BadRequest = 0x40,
        CaptureFailed = 0x80,
    }

    public class AudioBenchResult
    {
        public ushort PassMask;
        public ushort NoiseFloorDb;
        public int NTones;
        public int[] MagCounts = Array.Empty<int>();
        public int[] SnrDb = Array.Empty<int>();
        public bool[] PerTonePass = Array.Empty<bool>();

        /// <summary>The reply carries the shared engine's trailer (newer firmware).</summary>
        public bool HasTrailer;
        public int MicIndex;
        public AudioSelftestStatus Status;
        /// <summary>Signal level without DC, dBFS (a full-scale sine = 0); null = none measured.</summary>
        public double? LevelDbfs;
        public int Dc;
        public int Peak;
        public int ClippedSamples;
        public uint SampleRateHz;

        /// <summary>All requested tones cleared their SNR threshold.</summary>
        public bool Pass => NTones > 0 && PassMask == ((1 << NTones) - 1);

        /// <summary>The device captured nothing usable (likely a dead/disconnected mic).</summary>
        public bool NoSignal
        {
            get
            {
                if (HasTrailer) return (Status & AudioSelftestStatus.Stuck) != 0;
                if (NoiseFloorDb != 0) return false;
                foreach (int m in MagCounts) if (m != 0) return false;
                return true;
            }
        }
    }

    /// <summary>Bindable per-microphone result row for the health-check view.</summary>
    public class MicHealthResult : ReactiveObject
    {
        public int MicIndex { get; }
        public string MicLabel => $"Microphone {MicIndex + 1}";

        public MicHealthResult(int index) { MicIndex = index; }

        private string _status = "Not tested";
        public string Status { get => _status; set => this.RaiseAndSetIfChanged(ref _status, value); }

        private string _detail = string.Empty;
        public string Detail { get => _detail; set => this.RaiseAndSetIfChanged(ref _detail, value); }

        private IBrush _statusBrush = Brushes.Gray;
        public IBrush StatusBrush { get => _statusBrush; set => this.RaiseAndSetIfChanged(ref _statusBrush, value); }
    }
}
