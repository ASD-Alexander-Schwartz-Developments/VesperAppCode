using System;
using System.Buffers.Binary;

namespace VesperApp.Models
{
    /// <summary>
    /// Reply of console 79 <c>VESPER_MIC_LEVELS</c> (format 1, 50 bytes, little-endian): the sound trigger's
    /// live state and per-channel levels. Answered while recording with the console up and in Live Audio
    /// (where the trigger runs as a meter on the streamed samples). Contract: Kol docs/sound-trigger.md.
    /// </summary>
    public sealed class MicLevels
    {
        public const int MaxChannels = 4;
        private const short NoData = short.MinValue;

        public enum TriggerState { Off = 0, Listening = 1, Event = 2, Hold = 3 }

        public TriggerState State { get; private init; }
        public bool Running { get; private init; }
        public bool SourceLiveAudio { get; private init; }
        public bool Absolute { get; private init; }
        public bool BandOn { get; private init; }
        public bool HardwareRequested { get; private init; }
        /// <summary>The chain level is known, so dB SPL figures are valid.</summary>
        public bool SplValid { get; private init; }
        public int Channels { get; private init; }
        /// <summary>The background estimate has finished its warm-up.</summary>
        public bool Trained { get; private init; }
        public ushort Flags { get; private init; }
        /// <summary>Start / stop level as configured, dB (relative to the background, or dB SPL when <see cref="Absolute"/>).</summary>
        public double StartDb { get; private init; }
        public double StopDb { get; private init; }
        /// <summary>Chain level, dB; null when unknown.</summary>
        public double? ChainLevelDb { get; private init; }
        public uint SampleRate { get; private init; }
        public uint Events { get; private init; }
        public uint MissedHalves { get; private init; }
        public int LastChannel { get; private init; }

        /// <summary>Per channel, dBFS (null = no data): last sub-block level, background, peak since the last read.</summary>
        public double?[] Level { get; } = new double?[MaxChannels];
        public double?[] Background { get; } = new double?[MaxChannels];
        public double?[] Peak { get; } = new double?[MaxChannels];

        /// <summary>A trigger configuration is active (not just the Live Audio meter).</summary>
        public bool TriggerConfigured => Running && StartDb > 0;

        /// <summary>The level that starts an event on channel <paramref name="c"/>, dBFS; null when it cannot be
        /// placed (no trigger, background not known, or absolute without a known chain level).</summary>
        public double? StartDbfs(int c) => Threshold(c, StartDb);

        /// <summary>The level an event must stay under (for the hold) to end, dBFS; the stop level, or 3 dB
        /// below the start level when it is 0.</summary>
        public double? StopDbfs(int c) => Threshold(c, StopDb > 0 ? StopDb : StartDb - 3);

        private double? Threshold(int c, double db)
        {
            if (!TriggerConfigured || c < 0 || c >= MaxChannels) return null;
            if (Absolute)   /* dBFS = SPL - 94 - 26 + chain level (SPH0641LU4H: -26 dBFS at 94 dB SPL) */
                return ChainLevelDb is double chain ? db - 94 - 26 + chain : null;
            return Background[c] is double bg ? bg + db : null;
        }

        /// <summary>Parse a reply payload; null if it is not a format-1 reply.</summary>
        public static MicLevels? Parse(byte[]? p)
        {
            if (p == null || p.Length < 26 || p[0] != 1) return null;
            ReadOnlySpan<byte> s = p;
            short chain = BinaryPrimitives.ReadInt16LittleEndian(s[10..]);
            var m = new MicLevels
            {
                State = (TriggerState)(p[1] & 0x03),
                Running = (p[1] & 0x04) != 0,
                SourceLiveAudio = (p[1] & 0x08) != 0,
                Absolute = (p[1] & 0x10) != 0,
                BandOn = (p[1] & 0x20) != 0,
                HardwareRequested = (p[1] & 0x40) != 0,
                SplValid = (p[1] & 0x80) != 0,
                Channels = Math.Min(p[2], (byte)MaxChannels),
                Trained = p[3] != 0,
                Flags = BinaryPrimitives.ReadUInt16LittleEndian(s[4..]),
                StartDb = BinaryPrimitives.ReadInt16LittleEndian(s[6..]) / 100.0,
                StopDb = BinaryPrimitives.ReadInt16LittleEndian(s[8..]) / 100.0,
                ChainLevelDb = chain == NoData ? null : chain / 100.0,
                SampleRate = BinaryPrimitives.ReadUInt32LittleEndian(s[12..]),
                Events = BinaryPrimitives.ReadUInt32LittleEndian(s[16..]),
                MissedHalves = BinaryPrimitives.ReadUInt32LittleEndian(s[20..]),
                LastChannel = p[24],
            };
            for (int c = 0; c < m.Channels; c++)
            {
                int o = 26 + 6 * c;
                if (o + 6 > p.Length) break;
                m.Level[c] = Db(BinaryPrimitives.ReadInt16LittleEndian(s[o..]));
                m.Background[c] = Db(BinaryPrimitives.ReadInt16LittleEndian(s[(o + 2)..]));
                m.Peak[c] = Db(BinaryPrimitives.ReadInt16LittleEndian(s[(o + 4)..]));
            }
            return m;
        }

        private static double? Db(short v) => v == NoData ? null : v / 100.0;

        public string StateText => State switch
        {
            TriggerState.Listening => Trained ? "listening" : "learning the background",
            TriggerState.Event => "recording an event",
            TriggerState.Hold => "hold (sound ended, waiting)",
            _ => Running ? "meter only" : "off",
        };
    }
}
