using System.Globalization;
using VesperApp.Models;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Services;

public class UtilsTests
{
    [Theory]
    [InlineData((byte)0x00, (byte)0)]
    [InlineData((byte)0x09, (byte)9)]
    [InlineData((byte)0x10, (byte)10)]
    [InlineData((byte)0x59, (byte)59)]
    [InlineData((byte)0x99, (byte)99)]
    public void Bcd2Bin(byte bcd, byte value)
    {
        Assert.Equal(value, Utils.BCD2BIN(bcd));
        Assert.Equal(value, BinHeader.BCD2BIN(bcd));
    }

    [Fact]
    public void ByteHelpers_LittleEndianAndSigned()
    {
        byte[] b = { 0x78, 0x56, 0x34, 0x12, 0x00, 0x00, 0x80, 0xBF, 0xFE, 0xFF };
        Assert.Equal(0x12345678u, Utils.FromBytes(b, 0));
        Assert.Equal(-1.0f, Utils.FFromBytes(b, 4));
        Assert.Equal((short)-2, Utils.SFromBytes(b, 8));
        // 24-bit big-endian sample, left-aligned into an int32 (sign kept)
        Assert.Equal(unchecked((int)0x80000100), Utils.FromBytesS24bitMSB(new byte[] { 0x80, 0x00, 0x01 }, 0));
        Assert.Equal(0x12345600, Utils.FromBytesS24bitMSB(new byte[] { 0x12, 0x34, 0x56 }, 0));
    }

    [Fact]
    public void ExtractDateTimeSlowSensors_ReadsBcdDateTimeAndSubseconds()
    {
        // [flags] weekday, month, date, year, hours, minutes, seconds, tenth, subsecond u16; offset = the weekday byte
        byte[] b = { 0x00, 7, 0x10, 0x25, 0x26, 0x09, 0x30, 0x15, 0, 0xFF, 0x01, 0, 0, 0 };
        int used = Utils.ExtractDateTimeSlowSensors(b, 1, 1023, out DateTime dt);
        Assert.Equal(10, used);
        Assert.Equal(new DateTime(2026, 10, 25, 9, 30, 15, 500), dt);   // 1000 * (1023 - 511) / 1024
    }

    [Fact]
    public void ExtractDateTimeSlowSensors_TooShort_GivesMinValue()
    {
        Utils.ExtractDateTimeSlowSensors(new byte[5], 0, 1023, out DateTime dt);
        Assert.Equal(DateTime.MinValue, dt);
    }

    [Fact]
    public void Scan_ParsesNumbersAndStringsLikeSscanf()
    {
        var r = Utils.scan("M2026_10_25_09_30_00_996-M2026", "M%d_%d_%d_%d_%d_%d_%d");
        Assert.Equal(new object[] { 2026, 10, 25, 9, 30, 0, 996 }, r.Cast<object>().ToArray());

        var u = Utils.scan("2000-00-01 02:03:04", "%u-%u-%u %u:%u:%u");
        Assert.Equal(new object[] { 2000u, 0u, 1u, 2u, 3u, 4u }, u.Cast<object>().ToArray());

        var s = Utils.scan("id KOL 1.5", "id %s %f");
        Assert.Equal("KOL", s[0]);
        Assert.Equal(1.5, s[1]);

        Assert.Empty(Utils.scan("X2026", "M%d"));                 // literal mismatch stops the scan
    }

    [Fact]
    public void GetSeparator_FollowsCultureButNeverTheDecimalSeparator()
    {
        Assert.Equal(",", Utils.GetSeparator());                  // invariant (test default)

        CultureInfo saved = CultureInfo.CurrentCulture;
        try
        {
            var c = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            c.NumberFormat.NumberDecimalSeparator = ",";
            c.TextInfo.ListSeparator = ";";
            CultureInfo.CurrentCulture = c;
            Assert.Equal(";", Utils.GetSeparator());

            c = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            c.NumberFormat.NumberDecimalSeparator = ",";
            c.TextInfo.ListSeparator = ",";                       // would clash with decimals
            CultureInfo.CurrentCulture = c;
            Assert.Equal(";", Utils.GetSeparator());

            c = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            c.TextInfo.ListSeparator = ".";
            CultureInfo.CurrentCulture = c;
            Assert.Equal(",", Utils.GetSeparator());
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void KolTimestamp_PackedFieldsRoundTrip()
    {
        var t = new DateTime(2026, 10, 25, 9, 30, 15);
        uint ts = Kol.ToTimestamp(t);
        Assert.Equal((6u << 26) | (10u << 22) | (25u << 17) | (9u << 12) | (30u << 6) | 15u, ts);
        Assert.Equal(t.AddMilliseconds(500), Kol.FromTimestamp(ts, 512));        // ms field in 1/1024 s
    }

    [Fact]
    public void DecodeSummary_Text()
    {
        var s = new DecodeSummary { RawParsed = 2, Csv = 3, Image = 1, Failed = 1 };
        Assert.Equal("Decoded 2 raw file(s) → 3 CSV, 1 image · 1 failed.", s.ToString());
        Assert.Equal("Decoded 0 raw file(s) → nothing to decode.", new DecodeSummary().ToString());
    }
}
