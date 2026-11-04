using System;
using System.Linq;
using VesperApp.Models;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>DAQ24 (ADS1115) add-on: the VesperU5 ads1115.c record (asd-interfaces interfaces/daq24.md) and the
/// VESPER_TEST_DAQ bench reply.</summary>
public class Daq24Tests
{
    private static byte Bcd(int v) => (byte)(((v / 10) << 4) | (v % 10));

    /// <summary>One 20-byte record as the firmware writes it (BCD RTC fields, int16 LE codes).</summary>
    private static byte[] Record(byte flags, int h, int m, int s, ushort ssr, params short[] code)
    {
        var b = new byte[20];
        b[0] = 0x5A; b[1] = flags;
        b[2] = 3; b[3] = Bcd(11); b[4] = Bcd(4); b[5] = Bcd(26);        // weekday, month, date, year
        b[6] = Bcd(h); b[7] = Bcd(m); b[8] = Bcd(s); b[9] = 0;            // hours, minutes, seconds, tenth
        BitConverter.GetBytes(ssr).CopyTo(b, 10);
        for (int i = 0; i < 4; i++) BitConverter.GetBytes(code[i]).CopyTo(b, 12 + 2 * i);
        return b;
    }

    [Fact]
    public void Parser_DecodesVoltsAndSkipsInputsNotConverted()
    {
        byte[] data = Record(0x0F, 10, 20, 30, 1023, 8000, -800, 0, 32767)
            .Concat(Record(0x05, 10, 20, 31, 511, 4000, 1234, 16000, 99)).ToArray();
        using var p = new DAQ24Parser("x", data, 1023);

        Assert.Equal(2, p.Records.Count);
        DAQ24Record a = p.Records[0];
        Assert.Equal(new DateTime(2026, 11, 4, 10, 20, 30), a.Timestamp);
        Assert.Equal(1.0, a.Volts[0]!.Value, 6);                 // 8000 x 125 uV
        Assert.Equal(-0.1, a.Volts[1]!.Value, 6);
        Assert.Equal(4.095875, a.Volts[3]!.Value, 6);            // full scale

        DAQ24Record b = p.Records[1];
        Assert.Equal(0.5, b.Volts[0]!.Value, 6);
        Assert.Null(b.Volts[1]);                                 // flags bit 1 clear: not converted
        Assert.Equal(2.0, b.Volts[2]!.Value, 6);
        Assert.Null(b.Volts[3]);
        Assert.Equal(500, (b.Timestamp - new DateTime(2026, 11, 4, 10, 20, 31)).TotalMilliseconds, 0);
    }

    [Fact]
    public void Parser_ResyncsOnTheHeaderByte()
    {
        byte[] data = new byte[] { 0x00, 0x11 }.Concat(Record(0x01, 1, 2, 3, 1023, 100, 0, 0, 0)).ToArray();
        using var p = new DAQ24Parser("x", data, 1023);
        Assert.Single(p.Records);
        Assert.Equal(0.0125, p.Records[0].Volts[0]!.Value, 6);
    }

    private static byte[] Reply(byte status, byte sub, ushort idFound, int s0, int s1, int s2)
    {
        var b = new byte[20];
        b[0] = status; b[1] = sub;
        BitConverter.GetBytes(idFound).CopyTo(b, 2);
        BitConverter.GetBytes(s0).CopyTo(b, 8);
        BitConverter.GetBytes(s1).CopyTo(b, 12);
        BitConverter.GetBytes(s2).CopyTo(b, 16);
        return b;
    }

    private static int Pack(short lo, short hi) => (int)(((uint)(ushort)hi << 16) | (ushort)lo);

    [Fact]
    public void BenchReply_ShowsTheFourInputs()
    {
        var r = SensorBenchTest.ParseResponse(Reply(0, 0x0F, 0x48, Pack(8000, -800), Pack(0, 26400), 125));
        var (status, _, detail) = SensorBenchTest.Describe(MessageTypes.VESPER_TEST_DAQ, r);
        Assert.Equal("Pass", status);
        Assert.Equal("AIN0 1.000 V, AIN1 -0.100 V, AIN2 0.000 V, AIN3 3.300 V (ADS1115 at 0x48)", detail);
    }

    [Fact]
    public void BenchReply_ConversionMissing_IsCheck_AndAbsentBoard_IsNotFound()
    {
        var (status, _, detail) = SensorBenchTest.Describe(MessageTypes.VESPER_TEST_DAQ,
            SensorBenchTest.ParseResponse(Reply(2, 0x07, 0x49, Pack(1, 2), Pack(3, 4), 125)));
        Assert.Equal("Check", status);
        Assert.Contains("AIN3 -", detail);
        Assert.Contains("0x49", detail);

        (status, _, _) = SensorBenchTest.Describe(MessageTypes.VESPER_TEST_DAQ,
            SensorBenchTest.ParseResponse(Reply(1, 0, 0, 0, 0, 0)));
        Assert.Equal("Not found", status);
    }

    [Fact]
    public void VesperAndPipistrelle_OfferTheDriver()
    {
        Assert.Contains(Vesper.SupportedDeviceDrivers, d => d is ConfigADS1115Driver);
        Assert.Contains(Pipistrelle.SupportedDeviceDrivers, d => d is ConfigADS1115Driver);
    }
}
