using VesperApp.Models;
using Xunit;

namespace VesperApp.Tests.Models;

/// <summary>Firmware id / version from GET_VER (VesperU5 / KOL, id 15: [MAJOR, MINOR, UID0..3, type, 0]) and
/// Nanotag GET_INFO ([MAJOR, MID, MINOR, BLD, ...]); asd-interfaces interfaces/firmware-versions.md.</summary>
public class FirmwareVersionTests
{
    private static byte[] GetVer(byte major, byte minor, byte type) => new byte[] { major, minor, 0x78, 0x56, 0x34, 0x12, type, 0 };

    [Theory]
    [InlineData((byte)1, (byte)7, "1.7")]      // KOL 1.7 (was shown as "71")
    [InlineData((byte)1, (byte)8, "1.8")]
    [InlineData((byte)1, (byte)20, "1.20")]    // Vesper 1.20 (was "201")
    [InlineData((byte)1, (byte)22, "1.22")]
    public void GetVer_FirmwareIdIsMajorDotMinor(byte major, byte minor, string expected)
    {
        Version v = LoggerDevice.ParseGetVerFirmware(GetVer(major, minor, 3));
        Assert.Equal(new Version(major, minor), v);
        Assert.Equal(expected, LoggerDevice.FirmwareIdText(v));
    }

    [Theory]
    [InlineData((byte)7, false)]
    [InlineData((byte)8, true)]
    public void GetVer_KolGates_UseMajorMinor(byte minor, bool expected)
    {
        Version v = LoggerDevice.ParseGetVerFirmware(GetVer(1, minor, 3));
        Assert.Equal(expected, DeviceCapabilities.SupportsAudioSelftest(DeviceTypes.Kol, v));
        Assert.Equal(expected, DeviceCapabilities.SupportsSensorTest(DeviceTypes.Kol, v));
    }

    [Theory]
    [InlineData(DeviceTypes.Vesper, (byte)20, false)]
    [InlineData(DeviceTypes.Vesper, (byte)22, true)]
    [InlineData(DeviceTypes.Pipistrelle, (byte)20, false)]
    [InlineData(DeviceTypes.Pipistrelle, (byte)22, true)]
    public void GetVer_VesperGates_UseMajorMinor(DeviceTypes type, byte minor, bool expected)
    {
        Version v = LoggerDevice.ParseGetVerFirmware(GetVer(1, minor, (byte)type));
        Assert.Equal(expected, DeviceCapabilities.SupportsUsbModes(type, v));
        Assert.Equal(expected, DeviceCapabilities.SupportsAudioSelftest(type, v));
        Assert.Equal(expected, DeviceCapabilities.FlashesWithoutDock(type, v));
    }

    [Fact]
    public void Constructor_WithProbedFirmware_ShowsMajorDotMinor()
    {
        var d = new LoggerDevice("COM_TEST", 115200, DeviceTypes.Kol, 0x12345678, new Version(1, 7));
        Assert.Equal("1.7", d.FwId);
        Assert.Equal(new Version(1, 7), d.FirmwareVersion);
    }

    [Theory]
    [InlineData(new byte[] { 0, 0, 1, 39, 0, 1, 0, 0, 0, 0 }, 1, 39)]
    [InlineData(new byte[] { 0, 0, 1, 37, 0, 1, 0, 0, 0, 0 }, 1, 37)]
    public void NanotagGetInfo_ReleaseVersionIsMinorDotBuild(byte[] info, int major, int minor)
        => Assert.Equal(new Version(major, minor), LoggerDevice.NanotagFirmwareVersion(info));
}
