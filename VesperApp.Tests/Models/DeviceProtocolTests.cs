using Avalonia.Media;
using VesperApp.Models;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Models;

/// <summary>Live sensor test reply (console 71-74, asd-interfaces interfaces/sensor-test.md).</summary>
public class SensorBenchTestTests
{
    private static byte[] Reply(byte status, byte sub, ushort idFound, ushort idExpected, int s0, int s1, int s2)
    {
        var b = new byte[20];
        b[0] = status; b[1] = sub;
        BitConverter.GetBytes(idFound).CopyTo(b, 2);
        BitConverter.GetBytes(idExpected).CopyTo(b, 4);
        BitConverter.GetBytes(s0).CopyTo(b, 8);
        BitConverter.GetBytes(s1).CopyTo(b, 12);
        BitConverter.GetBytes(s2).CopyTo(b, 16);
        return b;
    }

    private static Color ColorOf(IBrush b) => Assert.IsAssignableFrom<ISolidColorBrush>(b).Color;

    [Fact]
    public void ParseResponse_ReadsAllFieldsLittleEndian()
    {
        SensorBenchResult r = SensorBenchTest.ParseResponse(Reply(3, 2, 0x6C, 0x6A, 1003, -250, int.MinValue))!;
        Assert.Equal(3, r.Status);
        Assert.Equal(2, r.SubStatus);
        Assert.Equal(0x6C, r.IdFound);
        Assert.Equal(0x6A, r.IdExpected);
        Assert.Equal(1003, r.Sample0);
        Assert.Equal(-250, r.Sample1);
        Assert.Equal(int.MinValue, r.Sample2);
    }

    [Fact]
    public void ParseResponse_ShortOrMissingReply_IsNull()
    {
        Assert.Null(SensorBenchTest.ParseResponse(null));
        Assert.Null(SensorBenchTest.ParseResponse(new byte[19]));
        Assert.NotNull(SensorBenchTest.ParseResponse(new byte[24]));
    }

    [Fact]
    public void BuildRequest_CarriesVariant() => Assert.Equal(new byte[] { 0, 2, 0, 0 }, SensorBenchTest.BuildRequest(2));

    [Theory]
    [InlineData((ushort)0x6A, "LSM6DSM")]
    [InlineData((ushort)0x69, "LSM6DS3")]
    [InlineData((ushort)0x6C, "LSM6DSO")]
    [InlineData((ushort)0xFA, "ICM-20680 (IMU10HT)")]
    [InlineData((ushort)0x12, "id 0x12")]
    public void ImuPart_NamesKnownIds(ushort id, string expected) => Assert.Equal(expected, SensorBenchTest.ImuPart(id));

    [Fact]
    public void Describe_NoAnswer()
    {
        var (status, brush, detail) = SensorBenchTest.Describe(MessageTypes.VESPER_TEST_IMU, null);
        Assert.Equal("No answer", status);
        Assert.Equal(Colors.OrangeRed, ColorOf(brush));
        Assert.Contains("did not answer", detail);
    }

    [Fact]
    public void Describe_ImuPass_ShowsPartAndAcceleration()
    {
        var r = SensorBenchTest.ParseResponse(Reply(SensorBenchTest.StatusOk, 0, 0x6A, 0x6A, 1003, 0, 0));
        var (status, brush, detail) = SensorBenchTest.Describe(MessageTypes.VESPER_TEST_IMU, r);
        Assert.Equal("Pass", status);
        Assert.Equal(Colors.SeaGreen, ColorOf(brush));
        Assert.Equal("LSM6DSM, acceleration 1.00 g (about 1 g at rest)", detail);
    }

    [Fact]
    public void Describe_TprhSampleFail_FlagsChecksum()
    {
        var r = SensorBenchTest.ParseResponse(Reply(SensorBenchTest.StatusSampleFail, 0, 0, 0, 2345, 4560, 0));
        var (status, brush, detail) = SensorBenchTest.Describe(MessageTypes.VESPER_TEST_TPRH, r);
        Assert.Equal("Check", status);
        Assert.Equal(Colors.Goldenrod, ColorOf(brush));
        Assert.EndsWith("23.45 °C, 45.6 %RH, checksum error", detail);
    }

    [Fact]
    public void Describe_SubFail_IsPartial()
    {
        var r = SensorBenchTest.ParseResponse(Reply(SensorBenchTest.StatusSubFail, 0, 0xFA, 0xFA, 990, 0, 0));
        var (status, _, detail) = SensorBenchTest.Describe(MessageTypes.VESPER_TEST_IMU, r);
        Assert.Equal("Partial", status);
        Assert.Contains("ICM-20680 (IMU10HT)", detail);
    }

    [Fact]
    public void Describe_IdFail_WrongIdVersusNothing()
    {
        var wrong = SensorBenchTest.Describe(MessageTypes.VESPER_TEST_IMU, SensorBenchTest.ParseResponse(Reply(1, 0, 0x33, 0x6A, 0, 0, 0)));
        Assert.Equal("Not found", wrong.status);
        Assert.Contains("Wrong device id 0x33", wrong.detail);

        var none = SensorBenchTest.Describe(MessageTypes.VESPER_TEST_IMU, SensorBenchTest.ParseResponse(Reply(1, 0, 0, 0x6A, 0, 0, 0)));
        Assert.Equal("Not found", none.status);
        Assert.Contains("No add-on of this type answered on the connector", none.detail);
    }

    [Fact]
    public void Describe_NotPresent_IsGrey()
    {
        var (status, brush, _) = SensorBenchTest.Describe(MessageTypes.VESPER_TEST_ALS, SensorBenchTest.ParseResponse(Reply(0xFF, 0, 0, 0, 0, 0, 0)));
        Assert.Equal("Not found", status);
        Assert.Equal(Colors.Gray, ColorOf(brush));
    }

    [Fact]
    public void Describe_UnknownStatus_ShowsNumber()
    {
        var (status, _, detail) = SensorBenchTest.Describe(MessageTypes.VESPER_TEST_ALS, SensorBenchTest.ParseResponse(Reply(7, 0, 0x3001, 0, 12345, 0, 0)));
        Assert.Equal("Status 7", status);
        Assert.Equal("123.45 lux (device id 0x3001)", detail);
    }

    [Fact]
    public void Describe_Exg_ShowsVariantAndExpectedId()
    {
        var (_, _, detail) = SensorBenchTest.Describe(MessageTypes.VESPER_TEST_EEG, SensorBenchTest.ParseResponse(Reply(0, 3, 0xD2, 0xD2, 0, 0, 0)));
        Assert.Equal("ADS1298 (EXG48), device id 0xD2 (expected 0xD2)", detail);
    }
}

/// <summary>Per-product capability gates (firmware thresholds KOL 1.8, Vesper / Pipistrelle 1.22).</summary>
public class DeviceCapabilitiesTests
{
    public static IEnumerable<object?[]> Gates() => new[]
    {
        new object?[] { DeviceTypes.Kol, null, false, true },
        new object?[] { DeviceTypes.Kol, new Version(1, 7), false, true },
        new object?[] { DeviceTypes.Kol, new Version(1, 8), true, true },
        new object?[] { DeviceTypes.Kol, new Version(1, 8, 3), true, true },
        new object?[] { DeviceTypes.Kol, new Version(2, 0), true, true },
        new object?[] { DeviceTypes.Vesper, null, false, false },
        new object?[] { DeviceTypes.Vesper, new Version(1, 20), false, false },
        new object?[] { DeviceTypes.Vesper, new Version(1, 21, 9), false, false },
        new object?[] { DeviceTypes.Vesper, new Version(1, 22), true, true },
        new object?[] { DeviceTypes.Pipistrelle, new Version(1, 21), false, false },
        new object?[] { DeviceTypes.Pipistrelle, new Version(1, 23), true, true },
        new object?[] { DeviceTypes.Nanotag, new Version(9, 9), false, false },
        new object?[] { null, new Version(9, 9), false, false },
    };

    /// <param name="tests">expected SupportsSensorTest and SupportsAudioSelftest</param>
    /// <param name="usbModes">expected SupportsUsbModes</param>
    [Theory]
    [MemberData(nameof(Gates))]
    public void FirmwareGates(DeviceTypes? product, Version? fw, bool tests, bool usbModes)
    {
        Assert.Equal(tests, DeviceCapabilities.SupportsSensorTest(product, fw));
        Assert.Equal(tests, DeviceCapabilities.SupportsAudioSelftest(product, fw));
        Assert.Equal(usbModes, DeviceCapabilities.SupportsUsbModes(product, fw));
    }

    [Theory]
    [InlineData(DeviceTypes.Kol, true, 4)]
    [InlineData(DeviceTypes.Vesper, true, 1)]
    [InlineData(DeviceTypes.Pipistrelle, true, 1)]
    [InlineData(DeviceTypes.Nanotag, false, 0)]
    public void AddonsAndMicrophones(DeviceTypes product, bool addons, int mics)
    {
        Assert.Equal(addons, DeviceCapabilities.HasAddonSensors(product));
        Assert.Equal(addons, DeviceCapabilities.HasUsbModes(product));
        Assert.Equal(mics, DeviceCapabilities.MicCount(product));
        Assert.Equal(mics, DeviceCapabilities.LiveAudioChannels(product));
        Assert.Equal(mics > 0, DeviceCapabilities.HasMicrophones(product));
    }

    [Fact]
    public void Misc()
    {
        Assert.False(DeviceCapabilities.HasAddonSensors(null));
        Assert.Equal(new[] { 1, 2, 4 }, DeviceCapabilities.MicCountOptions(DeviceTypes.Kol));
        Assert.Equal(new[] { 1 }, DeviceCapabilities.MicCountOptions(DeviceTypes.Vesper));
        Assert.Empty(DeviceCapabilities.MicCountOptions(DeviceTypes.Nanotag));
        Assert.True(DeviceCapabilities.HasGnss(DeviceTypes.Vesper));
        Assert.False(DeviceCapabilities.HasGnss(DeviceTypes.Kol));
        Assert.True(DeviceCapabilities.FlashesWithoutDock(DeviceTypes.Nanotag));
        Assert.True(DeviceCapabilities.FlashesWithoutDock(DeviceTypes.Kol));
        Assert.False(DeviceCapabilities.FlashesWithoutDock(DeviceTypes.Vesper, new Version(1, 20)));
        Assert.True(DeviceCapabilities.FlashesWithoutDock(DeviceTypes.Vesper, new Version(1, 22)));
        Assert.Equal("Vesper Wildlife Logger", DeviceCapabilities.LiveAudioDeviceName(DeviceTypes.Pipistrelle));
        Assert.Equal("device", DeviceCapabilities.DisplayName(null));
    }
}

/// <summary>VESPER_GET_USB_MODE reply (2 bytes, or 4 on KOL firmware with operating states).</summary>
public class KolUsbModeTests
{
    [Fact]
    public void Parse_TwoBytes_ModesOnly()
    {
        KolUsbModeState s = KolUsbModeState.Parse(new byte[] { 0, 2 })!;
        Assert.Equal(KolUsbMode.Idle, s.Active);
        Assert.Equal(KolUsbMode.LiveAudio, s.Pending);
        Assert.True(s.IsSwitching);
        Assert.False(s.HasStatus);
        Assert.Null(s.Flags);
        Assert.Null(s.Reason);
    }

    [Fact]
    public void Parse_FourBytes_FlagsAndReason()
    {
        KolUsbModeState s = KolUsbModeState.Parse(new byte[] { 2, 2, 0x4F, 6 })!;
        Assert.Equal(KolUsbMode.LiveAudio, s.Active);
        Assert.False(s.IsSwitching);
        Assert.True(s.HasStatus);
        Assert.Equal(KolStatusFlags.CardInserted | KolStatusFlags.CardMounted | KolStatusFlags.ConfigOk
                     | KolStatusFlags.RtcSet | KolStatusFlags.VbusPresent, s.Flags);
        Assert.Equal(KolRecordReason.NoClock, s.Reason);
    }

    [Fact]
    public void Parse_UnknownValues_FallBackSafely()
    {
        KolUsbModeState s = KolUsbModeState.Parse(new byte[] { 9, 1, 0x10, 200 })!;
        Assert.Equal(KolUsbMode.Idle, s.Active);                  // unknown mode -> Idle
        Assert.Equal(KolUsbMode.Sampling, s.Pending);
        Assert.Equal(KolStatusFlags.RecordReady, s.Flags);
        Assert.Equal(KolRecordReason.CardError, s.Reason);        // unknown reason -> card error
    }

    [Fact]
    public void Reason8_IsConfigRefused_NotCardError()
    {
        // KOL main 8c177c4 refuses a bad config.json whole and reports reason 8
        Assert.Equal(KolRecordReason.ConfigRefused, KolRecordReasonInfo.From(8));
        Assert.Equal(KolRecordReason.ConfigRefused, KolUsbModeState.Parse(new byte[] { 0, 0, 0, 8 })!.Reason);
        Assert.Equal("config.json refused", KolRecordReasonInfo.Short(KolRecordReason.ConfigRefused));
        Assert.StartsWith("config.json on the card is not a valid KOL configuration", KolRecordReasonInfo.Advice(KolRecordReason.ConfigRefused));
        Assert.Equal(KolRecordReason.CardError, KolRecordReasonInfo.From(9));          // still unknown
    }

    [Theory]
    [InlineData(0u, 0u)]
    [InlineData(1u, 1u)]
    [InlineData(1000u, 1000u)]
    [InlineData(1600u, 1000u)]
    public void Imu10ht_SampleRateEntry_LimitedTo1000Hz(uint entered, uint kept)
    {
        var d = new ConfigIMU10HTDriver { SampleRate1 = entered, SampleRate2 = entered };
        Assert.Equal(kept, d.SampleRate[1]);
        Assert.Equal(kept, d.SampleRate[2]);
    }


    [Fact]
    public void Parse_ThreeBytes_TreatedAsTwo()

    {
        KolUsbModeState s = KolUsbModeState.Parse(new byte[] { 1, 1, 0xFF })!;
        Assert.False(s.HasStatus);
        Assert.Equal(KolUsbMode.Sampling, s.Active);
    }

    [Fact]
    public void Parse_TooShort_IsNull()
    {
        Assert.Null(KolUsbModeState.Parse(null));
        Assert.Null(KolUsbModeState.Parse(new byte[] { 0 }));
    }

    [Fact]
    public void Labels()
    {
        Assert.True(KolUsbModeInfo.HasDisk(KolUsbMode.Idle));
        Assert.True(KolUsbModeInfo.HasDisk(null));
        Assert.False(KolUsbModeInfo.HasDisk(KolUsbMode.LiveAudio));
        Assert.Equal("unknown", KolUsbModeInfo.Label(null));
        Assert.Equal("SD card full", KolRecordReasonInfo.Short(KolRecordReason.DiskFull));
        Assert.Equal(string.Empty, KolRecordReasonInfo.Advice(KolRecordReason.Ok));
        foreach (KolRecordReason r in Enum.GetValues<KolRecordReason>().Where(r => r != KolRecordReason.Ok))
            Assert.NotEmpty(KolRecordReasonInfo.Advice(r));
    }
}
