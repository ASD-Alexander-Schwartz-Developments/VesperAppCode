using System;

namespace ASD.DeviceCore.Protocol
{
    /// <summary>
    /// One live thermal image from a Vesper / Pipistrelle Lepton add-on (VESPER_TEST_LEPTON op 9),
    /// as sent raw on the CDC console port: 32-byte "LVF1" header + width x height u16 pixels,
    /// little-endian. Spec: asd-interfaces interfaces/lepton-live.md.
    /// </summary>
    public sealed class ThermalFrame
    {
        public const byte FlagTLinear = 0x01, FlagTLinearHiRes = 0x02, FlagLepton3 = 0x04, FlagFfc = 0x08;

        public int Width { get; init; }
        public int Height { get; init; }
        public uint FrameNumber { get; init; }
        public uint UnitMs { get; init; }
        /// <summary>Focal-plane-array (sensor) temperature, °C; null when the unit had none.</summary>
        public double? FpaC { get; init; }
        public double? HousingC { get; init; }
        public byte Flags { get; init; }
        public ushort[] Pixels { get; init; } = Array.Empty<ushort>();

        public bool IsTLinear => (Flags & FlagTLinear) != 0;
        public bool IsLepton3 => (Flags & FlagLepton3) != 0;
        public bool FfcRan => (Flags & FlagFfc) != 0;
        /// <summary>Kelvin per count when <see cref="IsTLinear"/>.</summary>
        public double KelvinPerCount => (Flags & FlagTLinearHiRes) != 0 ? 0.01 : 0.1;

        public ushort At(int x, int y) => Pixels[y * Width + x];

        /// <summary>Pixel as °C (TLinear modules), else NaN: a raw count is not a temperature.</summary>
        public double Celsius(ushort v) => IsTLinear ? v * KelvinPerCount - 273.15 : double.NaN;
    }

    /// <summary>
    /// Splits LVF1 thermal frames out of the console byte stream. The reader hands it bytes only
    /// where the console parser is between messages and the byte is 'L' (no console message
    /// starts with it), then keeps feeding it until <see cref="Active"/> drops. A header with an
    /// unknown size or a CRC mismatch drops the frame (<see cref="Dropped"/>).
    /// </summary>
    public sealed class ThermalFrameDemux
    {
        public const int HeaderSize = 32;
        public const byte Magic0 = 0x4C;
        private static readonly byte[] Magic = { 0x4C, 0x56, 0x46, 0x31 };   // "LVF1"

        private readonly byte[] _hdr = new byte[HeaderSize];
        private int _hn;
        private byte[]? _payload;
        private int _pn;

        public bool Active => _hn > 0;
        public int Received { get; private set; }
        public int Dropped { get; private set; }

        /// <summary>Consume bytes of a frame; returns how many were used (at least 1 when the
        /// first byte starts a frame). <paramref name="frame"/> is set when one completes.</summary>
        public int Feed(ReadOnlySpan<byte> data, out ThermalFrame? frame)
        {
            frame = null;
            int i = 0;
            while (i < data.Length)
            {
                if (_hn < HeaderSize)
                {
                    byte b = data[i];
                    if (_hn < 4 && b != Magic[_hn])
                    {
                        // not a frame: leave this byte to the console parser (but always make progress)
                        bool started = _hn > 0;
                        Reset();
                        return started ? i : i + 1;
                    }
                    _hdr[_hn++] = b;
                    i++;
                    if (_hn == HeaderSize && !BeginPayload())
                    {
                        Dropped++;
                        Reset();
                        return i;
                    }
                    continue;
                }

                int n = Math.Min(data.Length - i, _payload!.Length - _pn);
                data.Slice(i, n).CopyTo(_payload.AsSpan(_pn));
                _pn += n;
                i += n;
                if (_pn == _payload.Length)
                {
                    frame = Finish();
                    if (frame == null) Dropped++; else Received++;
                    Reset();
                    return i;
                }
            }
            return i;
        }

        private bool BeginPayload()
        {
            int w = U16(4), h = U16(6);
            uint len = U32(24);
            bool sizeOk = (w == 160 && h == 120) || (w == 80 && h == 60);
            if (!sizeOk || len != (uint)(w * h * 2)) return false;
            _payload = new byte[len];
            _pn = 0;
            return true;
        }

        private ThermalFrame? Finish()
        {
            if (Crc32(_payload!) != U32(28)) return null;
            int w = U16(4), h = U16(6);
            var px = new ushort[w * h];
            for (int k = 0; k < px.Length; k++)
                px[k] = (ushort)(_payload![2 * k] | (_payload[2 * k + 1] << 8));
            short fpa = (short)U16(16), aux = (short)U16(18);
            return new ThermalFrame
            {
                Width = w,
                Height = h,
                FrameNumber = U32(8),
                UnitMs = U32(12),
                FpaC = fpa == short.MinValue ? null : fpa / 100.0,
                HousingC = aux == short.MinValue ? null : aux / 100.0,
                Flags = _hdr[20],
                Pixels = px,
            };
        }

        private void Reset() { _hn = 0; _payload = null; _pn = 0; }

        private int U16(int o) => _hdr[o] | (_hdr[o + 1] << 8);
        private uint U32(int o) => (uint)(_hdr[o] | (_hdr[o + 1] << 8) | (_hdr[o + 2] << 16) | (_hdr[o + 3] << 24));

        private static readonly uint[] CrcTable = BuildTable();
        private static uint[] BuildTable()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[n] = c;
            }
            return t;
        }

        /// <summary>CRC-32 as zlib (IEEE 802.3, reflected), as the firmware computes it.</summary>
        public static uint Crc32(ReadOnlySpan<byte> data)
        {
            uint c = 0xFFFFFFFFu;
            foreach (byte b in data) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
            return c ^ 0xFFFFFFFFu;
        }
    }
}
