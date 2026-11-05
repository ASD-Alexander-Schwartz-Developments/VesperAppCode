using VesperApp.Models;
using Xunit;

namespace VesperApp.Tests.Models;

/// <summary>GETFLAGS [32..33] battery as the Devices panel shows it (LoggerDevice.BatteryText).</summary>
public class BatteryTextTests
{
    [Theory]
    [InlineData(0u, "—")]                    // no reading yet
    [InlineData(4210u, "4.21 V")]            // Vesper / Pipistrelle cell
    [InlineData(3406u, "3.41 V")]            // KOL node
    [InlineData(0x8000u | 3896u, "≥ 3.90 V")] // KOL above its range (USB, fresh cells): a lower bound
    [InlineData(0x8000u, "—")]
    public void Shows_value_and_lower_bound(uint raw, string expected)
        => Assert.Equal(expected, LoggerDevice.BatteryText(raw));
}
