using VesperApp.Models;
using Xunit;

namespace VesperApp.Tests.Models;

/// <summary>
/// Per-device "bitmask" (firmware controlBitmask) bit positions. They are the firmware's, shared by
/// VesperU5, KOL and Nanotag (asd-interfaces vesper-schedule.md "Per-device bitmask"): LED = bit 1
/// (VesperU5 LED_Pin = GPIO_PIN_1, Nanotag CFG_FLAG_LED_SAMPLING), IMU LED = 0x20 (IMU_LED_ACTIVE),
/// Nanotag GNSS trigger 0x1000 (BIT_GPS_TRIGGER_MODE), Nanotag acc motion 0x2000 (BIT_ACC_TRIGGER_MODE).
/// </summary>
public class ControlBitmaskTests
{
    public static IEnumerable<object[]> LedBit1Drivers() => new[]
    {
        new object[] { new ConfigACLYSDriver() },       // Nanotag GNSS (was read as 0x01 by Nanotag <= 1.39)
        new object[] { new ConfigTPRH31Driver() },
        new object[] { new ConfigALS3001DDriver() },
        new object[] { new ConfigSPH0641Driver() },
        new object[] { new ConfigKOLDriver() },
        new object[] { new ConfigLeptonDriver() },
    };

    [Theory]
    [MemberData(nameof(LedBit1Drivers))]
    public void LedActivity_IsBit1(ConfigurationDeviceDriver d)
    {
        d.Bitmask = 0;
        d.IsLEDActive = true;
        Assert.Equal(0x02u, d.Bitmask);
        d.IsLEDActive = false;
        Assert.Equal(0u, d.Bitmask);
    }

    [Fact]
    public void ImuLedActivity_Is0x20()
    {
        var a = new ConfigIMU10Driver { Bitmask = 0 };
        a.IsLEDActive = true;
        Assert.Equal(0x20u, a.Bitmask);
        var b = new ConfigIMU10HTDriver { Bitmask = 0 };
        b.IsLEDActive = true;
        Assert.Equal(0x20u, b.Bitmask);
    }

    [Fact]
    public void NanotagTriggerBits()
    {
        var g = new ConfigACLYSDriver();
        g.EnableGPSTrigger = true;
        Assert.Equal(0x1000u, g.Bitmask);

        var a = new ConfigNanotagAcc();
        a.EnableTrigger = true;
        Assert.Equal(0x2000u, a.Bitmask);
    }
}
