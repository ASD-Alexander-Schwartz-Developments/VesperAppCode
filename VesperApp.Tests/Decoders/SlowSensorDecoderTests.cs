using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Decoders;

/// <summary>TPRH31 (TPHParser, record 0x59) and ALS3001D (ALSParser, record 0x5A) on synthetic records
/// built from the firmware layouts (VesperU5 TPRH31.c / ALS3001D.h als_sample_t).</summary>
public class SlowSensorDecoderTests
{
    /// <summary>Header, flags, rtc_date_t (weekday, month, date, year BCD), rtc_time_t (h, m, s BCD, tenth), subsecond u16.</summary>
    private static byte[] Stamp(byte header, DateTime t, ushort subsec)
        => new byte[]
        {
            header, 0,
            7, Raw.Bcd(t.Month), Raw.Bcd(t.Day), Raw.Bcd(t.Year - 2000),
            Raw.Bcd(t.Hour), Raw.Bcd(t.Minute), Raw.Bcd(t.Second), 0,
            (byte)subsec, (byte)(subsec >> 8),
        };

    /// <summary>18-byte TPRH31 record: SHT31 temperature and humidity, big-endian, each followed by its CRC byte.</summary>
    private static byte[] Tprh(DateTime t, ushort subsec, ushort rawT, ushort rawRh)
        => Raw.Concat(Stamp(0x59, t, subsec),
                      new byte[] { (byte)(rawT >> 8), (byte)rawT, 0xCC, (byte)(rawRh >> 8), (byte)rawRh, 0xCC });

    /// <summary>14-byte ALS record: OPT3001 result register (exponent 4 bits, mantissa 12 bits), little-endian.</summary>
    private static byte[] Als(DateTime t, ushort subsec, ushort value)
        => Raw.Concat(Stamp(0x5A, t, subsec), new byte[] { (byte)value, (byte)(value >> 8) });

    private static readonly DateTime T0 = new(2026, 10, 25, 9, 30, 15);

    [Fact]
    public void Tph_DecodesTemperatureHumidityAndRecordTimestamps_IncludingLastRecord()
    {
        using var dir = new TempDir();
        string f = dir.File("R2026_10_25_09_30_15_000-R2026_10_25_09_31_15_000.RBN");
        byte[] data = Raw.Concat(
            Tprh(T0, 1023, 0x6666, 0x8000),                       // 25.00 C, 50.00 %RH, 0 ms
            Tprh(T0.AddMinutes(1), 511, 0x0000, 0xFFFF));          // -45.00 C, 100.00 %RH, 500 ms - ends the data
        Assert.Equal(36, data.Length);

        using (var p = new TPHParser(f, data, DateTime.Now, 1023, 60000)) p.WriteFile();
        string[] lines = Fixtures.WaitForLines(f + ".csv", 3);

        Assert.Equal(new[]
        {
            "Time,Temperature [C],Relative Humidity [%]",
            "10/25/2026 09:30:15,25.00,50.00",
            "10/25/2026 09:31:15.5,-45.00,100.00",
        }, lines);
    }

    [Fact]
    public void Tph_SkipsBytesUntilRecordHeader()
    {
        using var dir = new TempDir();
        string f = dir.File("x.RBN");
        byte[] data = Raw.Concat(new byte[] { 0x00, 0xFF, 0x12 }, Tprh(T0, 1023, 0x6666, 0x8000), new byte[] { 0 });
        using (var p = new TPHParser(f, data, DateTime.Now, 1023, 0)) p.WriteFile();
        string[] lines = Fixtures.WaitForLines(f + ".csv", 2);
        Assert.Equal(2, lines.Length);
        Assert.Equal("10/25/2026 09:30:15,25.00,50.00", lines[1]);
    }

    [Fact]
    public void Als_DecodesLuxFromExponentAndMantissa_IncludingLastRecord()
    {
        using var dir = new TempDir();
        string f = dir.File("L2026_10_25_09_30_15_000-L2026_10_25_09_30_17_000.LBN");
        byte[] data = Raw.Concat(
            Als(T0, 1023, 0x0064),                                // E 0, R 100  -> 1.00 lux
            Als(T0.AddSeconds(1), 1023, 0x2123),                  // E 2, R 291  -> 11.64 lux
            Als(T0.AddSeconds(2), 511, 0xB800));                  // E 11, R 2048 -> 41943.04 lux - ends the data
        Assert.Equal(42, data.Length);

        using (var p = new ALSParser(f, data, DateTime.Now, 1023, 1000)) p.WriteFile();
        string[] lines = Fixtures.WaitForLines(f + ".csv", 4);

        Assert.Equal(new[]
        {
            "Time,Lux",
            "10/25/2026 09:30:15,1.00",
            "10/25/2026 09:30:16,11.64",
            "10/25/2026 09:30:17.5,41943.04",
        }, lines);
    }

    [Fact]
    public void EmptyOrNullData_Throws()
    {
        Assert.Throws<ArgumentException>(() => new TPHParser("x", Array.Empty<byte>(), DateTime.Now, 1023, 0));
        Assert.Throws<ArgumentException>(() => new ALSParser("x", Array.Empty<byte>(), DateTime.Now, 1023, 0));
        Assert.Throws<ArgumentNullException>(() => new TPHParser("x", null!, DateTime.Now, 1023, 0));
        Assert.Throws<ArgumentNullException>(() => new ALSParser("x", null!, DateTime.Now, 1023, 0));
    }
}
