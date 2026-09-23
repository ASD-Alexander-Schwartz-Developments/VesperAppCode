using System;

namespace VesperApp.Models
{
    /// <summary>
    /// KOL microphone (MDF/PDM) parameters as the firmware stores them in the
    /// config.json device entry: gain in ~3 dB steps (-16..24), high-pass filter
    /// selector (0 off, 1..4 = cutoff 0.000625/0.00125/0.0025/0.0095 x fs),
    /// reshape filter word (bit 8 enable, bit 0: 1 = ratio 1, 0 = ratio 4) and the
    /// CIC base filter (0 SINC5, 1 SINC4). Wire format = VESPER_SET_MIC_PARAMS payload.
    /// </summary>
    public sealed record KolMicParams(int GainStep, int Hpf, bool ReshapeEnabled, bool ReshapeRatioOne, bool Cic4)
    {
        public const int GainMin = -16, GainMax = 24;

        public double GainDb => GainStep * 3.0;

        public int DFilter => (ReshapeEnabled ? 0x100 : 0) | (ReshapeRatioOne ? 0x1 : 0);

        /// <summary>The deployed defaults (params.c): +36 dB, HPF lowest cutoff, reshape /4, SINC5.</summary>
        public static KolMicParams Default => new(12, 1, true, false, false);

        public byte[] Pack()
        {
            int g = Math.Clamp(GainStep, GainMin, GainMax);
            int d = DFilter;
            return new[] { (byte)(sbyte)g, (byte)Hpf, (byte)(d & 0xFF), (byte)(d >> 8), (byte)(Cic4 ? 1 : 0) };
        }

        public static KolMicParams? Parse(ReadOnlySpan<byte> b)
        {
            if (b.Length < 5) return null;
            int d = b[2] | (b[3] << 8);
            return new KolMicParams((sbyte)b[0], b[1], (d & 0x100) != 0, (d & 0x1) != 0, b[4] != 0);
        }

        public static readonly string[] HpfLabels = { "Off", "0.06 % of fs", "0.125 % of fs", "0.25 % of fs", "0.95 % of fs" };
    }

    /// <summary>VESPER_GET_MIC_PARAMS reply: parameters + Live Audio status.</summary>
    public sealed record KolMicStatus(KolMicParams Params, bool Streaming, bool Capturing, bool Saturated, uint SampleRate)
    {
        public static KolMicStatus? Parse(byte[]? b)
        {
            if (b == null || b.Length < 12) return null;
            KolMicParams? p = KolMicParams.Parse(b);
            if (p == null) return null;
            uint fs = (uint)(b[8] | (b[9] << 8) | (b[10] << 16) | (b[11] << 24));
            return new KolMicStatus(p, b[5] != 0, b[6] != 0, b[7] != 0, fs);
        }
    }
}
