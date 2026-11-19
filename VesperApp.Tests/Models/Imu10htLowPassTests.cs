using VesperApp.Models;
using Xunit;

namespace VesperApp.Tests.Models;

/// <summary>
/// IMU10HT low-pass filter in config.json "imuRange" bits 8..11 (VesperU5 after 1.22, imu10ht_cfg.h; D-16): bit 11
/// set = DLPF_CFG from bits 8..10. The gyro range (bits 0..2) and accel range (bits 16..17) keep their places, and
/// changing one setting must not clear another.
/// </summary>
public class Imu10htLowPassTests
{
    [Fact]
    public void Off_LeavesImuRangeAsBefore()
    {
        var d = new ConfigIMU10HTDriver();
        Assert.Equal(0u, d.ImuRange);
        Assert.Equal((ushort)0, d.LowPassFilter.Value);
        Assert.Equal("Off", d.LowPassFilter.ToString());
    }

    [Theory]
    [InlineData(1, 0x900u, "176 Hz")]
    [InlineData(3, 0xB00u, "41 Hz")]
    [InlineData(6, 0xE00u, "5 Hz")]
    public void Filter_SetsBit11AndTheValue(ushort v, uint bits, string label)
    {
        var d = new ConfigIMU10HTDriver { LowPassFilter = IMU10HTLowPass.CreateFromValue(v) };
        Assert.Equal(bits, d.ImuRange);
        Assert.Equal(v, d.LowPassFilter.Value);
        Assert.Equal(label, d.LowPassFilter.ToString());
    }

    [Fact]
    public void Ranges_AndFilter_DoNotClearEachOther()
    {
        var d = new ConfigIMU10HTDriver();
        d.LowPassFilter = IMU10HTLowPass.CreateFromValue(2);
        d.GyroRange = IMU10HTGyroRanges.CreateFromValue(IMU10HTGyroRanges.IMU10HT_D1000N);
        d.AccRange = IMU10HTAccRanges.CreateFromValue(IMU10HTAccRanges.IMU10HT_G8N);
        Assert.Equal((2u << 16) | (1u << 11) | (2u << 8) | 2u, d.ImuRange);
        Assert.Equal(IMU10HTGyroRanges.IMU10HT_D1000N, d.GyroRange.Value);   // the filter bits are not read as range
        d.LowPassFilter = IMU10HTLowPass.CreateFromValue(0);
        Assert.Equal((2u << 16) | 2u, d.ImuRange);
    }

    [Fact]
    public void Loaded_ImuRange_ShowsTheFilter()
    {
        var d = new ConfigIMU10HTDriver { ImuRange = (1u << 16) | (1u << 11) | (4u << 8) | 3u };
        Assert.Equal((ushort)4, d.LowPassFilter.Value);
        Assert.Equal(IMU10HTGyroRanges.IMU10HT_D2000N, d.GyroRange.Value);
        Assert.Equal(IMU10HTAccRanges.IMU10HT_G4N, d.AccRange.Value);
    }
}
