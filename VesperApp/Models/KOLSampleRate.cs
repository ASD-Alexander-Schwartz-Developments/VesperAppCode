using System;
using System.Globalization;
using System.Linq;
using VesperApp.Services;

namespace VesperApp.Models
{
    /// <summary>
    /// KOL microphone sample rate choice. The 4.8 MHz microphone clock divides exactly to most of these;
    /// the 44.1 kHz family and 250 kHz run at the closest rate it allows (shown in the label, recorded in
    /// the file header). The MDF cannot go below 4.8 MHz / 2048 ≈ 2.34 kHz.
    /// </summary>
    public sealed class KOLSampleRate
    {
        private static readonly KOLSampleRate[] listOfOptions = MicChainSolver.CommonRates.Select(r => new KOLSampleRate(r)).ToArray();

        public static KOLSampleRate[] ListOfOptions => listOfOptions;

        public static KOLSampleRate CreateFromValue(uint v) => listOfOptions.FirstOrDefault(o => o.Value == v) ?? new KOLSampleRate(v);

        public KOLSampleRate(uint value) { Value = value; }

        public uint Value { get; }

        public override string ToString()
        {
            if (Value == 0) return "Off";
            MicChainSolver.Chain? c = MicChainSolver.Solve(Value, 0);
            string s = MicChainSolver.Hz(Value);
            return c is { Inexact: true }
                ? s + $"  (runs at {c.ExactHz.ToString("0.#", CultureInfo.InvariantCulture)} Hz)"
                : s;
        }
    }
}
