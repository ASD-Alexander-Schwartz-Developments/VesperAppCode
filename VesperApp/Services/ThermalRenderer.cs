using System;
using System.Globalization;
using System.IO;
using System.Text;
using ASD.DeviceCore.Protocol;

namespace VesperApp.Services
{
    /// <summary>
    /// Live thermal view (Device Tests › Thermal Camera): maps a <see cref="ThermalFrame"/> through the
    /// same palettes as the recorded Lepton images (LeptonParser) into BGRA pixels, and formats pixel
    /// values (°C on radiometric modules, raw counts otherwise). Pure, unit-tested.
    /// </summary>
    public static class ThermalRenderer
    {
        public static byte[] Palette(LeptonFilterType p) => p switch
        {
            LeptonFilterType.LEPTON_GRAYSCALE => LeptonVoipSegment.colormap_grayscale,
            LeptonFilterType.LEPTON_IRONBLACK => LeptonVoipSegment.colormap_ironblack,
            _ => LeptonVoipSegment.colormap_rainbow,
        };

        /// <summary>Coldest and hottest pixel (the auto range).</summary>
        public static (ushort lo, ushort hi) Range(ushort[] px)
        {
            if (px.Length == 0) return (0, 0);
            ushort lo = ushort.MaxValue, hi = 0;
            foreach (ushort v in px)
            {
                if (v < lo) lo = v;
                if (v > hi) hi = v;
            }
            return (lo, hi);
        }

        /// <summary>Fill <paramref name="bgra"/> (4 bytes per pixel, opaque) from the pixels,
        /// scaling lo..hi onto the 256 palette entries.</summary>
        public static void ToBgra(ushort[] px, ushort lo, ushort hi, LeptonFilterType palette, byte[] bgra)
        {
            byte[] map = Palette(palette);
            int span = Math.Max(1, hi - lo);
            for (int i = 0; i < px.Length; i++)
            {
                int v = (px[i] - lo) * 255 / span;
                if (v < 0) v = 0; else if (v > 255) v = 255;
                bgra[4 * i] = map[3 * v + 2];
                bgra[4 * i + 1] = map[3 * v + 1];
                bgra[4 * i + 2] = map[3 * v];
                bgra[4 * i + 3] = 255;
            }
        }

        public static string Format(ThermalFrame f, ushort v) =>
            f.IsTLinear ? f.Celsius(v).ToString("F1", CultureInfo.InvariantCulture) + " °C"
                        : v.ToString(CultureInfo.InvariantCulture) + " (raw)";

        /// <summary>Snapshot table: one row per image row, °C (TLinear) or raw counts, comma-separated.</summary>
        public static string ToCsv(ThermalFrame f)
        {
            var sb = new StringBuilder();
            sb.Append("# Lepton ").Append(f.IsLepton3 ? "3.x" : "2.x").Append(' ').Append(f.Width).Append('x').Append(f.Height)
              .Append(", frame ").Append(f.FrameNumber)
              .Append(f.IsTLinear ? ", deg C" : ", raw counts (module is not radiometric)");
            if (f.FpaC.HasValue) sb.Append(", sensor ").Append(f.FpaC.Value.ToString("F2", CultureInfo.InvariantCulture)).Append(" C");
            sb.Append('\n');
            for (int y = 0; y < f.Height; y++)
            {
                for (int x = 0; x < f.Width; x++)
                {
                    if (x > 0) sb.Append(',');
                    ushort v = f.At(x, y);
                    sb.Append(f.IsTLinear ? f.Celsius(v).ToString("F2", CultureInfo.InvariantCulture)
                                          : v.ToString(CultureInfo.InvariantCulture));
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
