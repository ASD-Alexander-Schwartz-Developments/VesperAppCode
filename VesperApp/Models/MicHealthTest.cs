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
    /// A mic passes when every requested tone clears its SNR threshold.
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

            return new AudioBenchResult
            {
                PassMask = passMask,
                NoiseFloorDb = noiseFloorDb,
                NTones = n,
                MagCounts = mags,
                SnrDb = snr,
                PerTonePass = perTone,
            };
        }
    }

    public class AudioBenchResult
    {
        public ushort PassMask;
        public ushort NoiseFloorDb;
        public int NTones;
        public int[] MagCounts = Array.Empty<int>();
        public int[] SnrDb = Array.Empty<int>();
        public bool[] PerTonePass = Array.Empty<bool>();

        /// <summary>All requested tones cleared their SNR threshold.</summary>
        public bool Pass => NTones > 0 && PassMask == ((1 << NTones) - 1);

        /// <summary>The device captured nothing usable (likely a dead/disconnected mic).</summary>
        public bool NoSignal
        {
            get
            {
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
