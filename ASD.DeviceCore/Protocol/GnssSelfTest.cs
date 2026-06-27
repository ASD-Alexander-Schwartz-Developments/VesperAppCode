namespace ASD.DeviceCore.Protocol
{
    /// <summary>
    /// GNSS front-end bench test (VESPER_TEST_GPS) request/response packing — the
    /// in-RAM go/no-go contract shared by the VesperU5 firmware (bench_gps_run on
    /// the feature/dock-bench-test branch), the VesperApp Device Tests panel, and
    /// the factory suite. The host (pluto-gnss) injects a CW tone near L1 into a
    /// shielded path; the device captures a short snapshot, detects the tone, and
    /// replies front-end go/no-go.
    ///
    /// Request (gps_test_req_t, 12 bytes, little-endian):
    ///   u32 snapSize (ms hint; 0 = FW default 128); u32 expectedPRNmask;
    ///   u8  cn0ThreshDb; u8[3] reserved
    /// Response (gps_test_resp_t, 12 bytes):
    ///   u8  status (0=ok, 0xFF=fail); u8 nAcquired; u16 peakCN0;
    ///   u32 acquiredPRNmask; i32 tcxoOffset (Hz)
    ///
    /// Today the FW runs an RF-chain tone go/no-go, so peakCN0 is a tone-SNR proxy
    /// and nAcquired is "tone seen" — full PRN acquisition is future FW work.
    /// </summary>
    public static class GpsBenchTest
    {
        /// <summary>AsdFrame message type — matches the FW MSG enum VESPER_TEST_GPS
        /// (dock bench-test family, AUDIO=60 base) and VesperApp MessageTypes.</summary>
        public const byte VesperTestGps = 61;

        public static byte[] BuildRequest(uint snapSizeMs, byte cn0ThreshDb, uint expectedPrnMask = 0)
        {
            byte[] b = new byte[12];
            int o = 0;
            b[o++] = (byte)snapSizeMs;          b[o++] = (byte)(snapSizeMs >> 8);
            b[o++] = (byte)(snapSizeMs >> 16);  b[o++] = (byte)(snapSizeMs >> 24);
            b[o++] = (byte)expectedPrnMask;         b[o++] = (byte)(expectedPrnMask >> 8);
            b[o++] = (byte)(expectedPrnMask >> 16); b[o++] = (byte)(expectedPrnMask >> 24);
            b[o++] = cn0ThreshDb;
            // b[9..11] reserved = 0
            return b;
        }

        public static GpsTestResult? ParseResponse(byte[]? payload)
        {
            if (payload == null || payload.Length < 12) return null;
            int o = 0;
            byte status = payload[o++];
            byte nAcquired = payload[o++];
            ushort peakCN0 = (ushort)(payload[o++] | (payload[o++] << 8));
            uint prnMask = (uint)(payload[o++] | (payload[o++] << 8) | (payload[o++] << 16) | (payload[o++] << 24));
            int tcxo = payload[o++] | (payload[o++] << 8) | (payload[o++] << 16) | (payload[o++] << 24);
            return new GpsTestResult
            {
                Status = status,
                NAcquired = nAcquired,
                PeakCN0 = peakCN0,
                AcquiredPrnMask = prnMask,
                TcxoOffsetHz = tcxo,
            };
        }
    }

    public sealed class GpsTestResult
    {
        public byte Status;             // 0 = ran, 0xFF = capture failed
        public byte NAcquired;          // tone seen (>=1) for the interim tone test
        public ushort PeakCN0;          // front-end signal level (dB; tone-SNR proxy today)
        public uint AcquiredPrnMask;
        public int TcxoOffsetHz;        // measured tone offset (Hz)
        public bool Ok => Status == 0;                       // front end responded
        public bool Pass => Status == 0 && NAcquired >= 1;   // signal above threshold
    }
}
