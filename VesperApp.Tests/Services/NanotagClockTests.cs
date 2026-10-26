using System.Diagnostics;
using VesperApp.Models;
using VesperApp.Services.Time;
using VesperApp.ViewModels;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>Nanotag clock (firmware 1.37+, asd-interfaces interfaces/rtc-time-sync.md section Nanotag): no sub-second
/// shift, SET_DATETIME writes whole seconds and keeps the second boundary, RTC_CAL reply carries FREQCORR.</summary>
public class NanotagClockTests
{
    /// <summary>Simulated Nanotag: calendar runs Offset ahead of true time; Set keeps the fractional phase.</summary>
    private sealed class FakeNanotag : IRtcDevice
    {
        private readonly ReferenceClock _rc;
        public double Offset;
        public int Shifts, Sets;
        public const double OneWay = 0.0004;

        public FakeNanotag(ReferenceClock rc) => _rc = rc;
        public string UnitKey => "NANO";
        public bool CanShift => false;
        public bool StoresCalibration => true;

        private double TrueNow() => RtcSyncService.DeviceSecondsOfTrue(_rc.TrueUtcNow(), true);

        public Task<RtcExchange?> TimeSyncAsync(uint seq, int timeoutMs = 1000)
        {
            long t1 = Stopwatch.GetTimestamp();
            long one = (long)(OneWay * Stopwatch.Frequency);
            double rx = RtcSyncService.DeviceSecondsOfTrue(_rc.TrueUtcAt(t1 + one), true) + Offset;
            rx = Math.Floor(rx * 1024) / 1024 + 32 / 65536.0;          // 1/1024 s stamp, middle of the tick
            return Task.FromResult<RtcExchange?>(new RtcExchange(seq, t1, t1 + 2 * one, rx, rx, 22.0, 5, 1000, RtcCalFlags.UnitCalibration));
        }

        public Task<bool> SetRtcAsync(DateTime deviceTime, int timeoutMs = 2000)
        {
            Sets++;
            double t = TrueNow() + OneWay;
            double dev = t + Offset;
            double phase = dev - Math.Floor(dev);
            Offset = DeviceEpoch.ToSeconds(deviceTime) + phase - t;      // whole seconds written, phase kept
            return Task.FromResult(true);
        }

        public Task<bool> ShiftAsync(int microseconds, int timeoutMs = 2000) { Shifts++; return Task.FromResult(false); }
        public Task<RtcCalStatus?> CalAsync(byte[] payload, int timeoutMs = 2000) => Task.FromResult<RtcCalStatus?>(null);
    }

    [Theory]
    [InlineData(3.2, 0.2)]
    [InlineData(-2.7, 0.3)]
    [InlineData(0.4, 0.4)]
    [InlineData(-0.6, 0.4)]
    public void PlanWholeSecondSet_RemovesWholeSeconds_AtMidSecond(double offset, double residual)
    {
        double now = 846_000_000.123;
        var p = RtcSyncService.PlanWholeSecondSet(now, offset);
        Assert.Equal(residual, p.Residual, 9);
        Assert.Equal(0.5, p.ArriveDeviceSeconds - Math.Floor(p.ArriveDeviceSeconds), 9);
        Assert.True(p.ArriveDeviceSeconds - (now + offset) >= 0.25);
        Assert.Equal(Math.Floor(p.ArriveDeviceSeconds) - Math.Round(offset, MidpointRounding.AwayFromZero), p.SetSeconds);
    }

    [Theory]
    [InlineData(3.2, 0.2, 1)]
    [InlineData(-2.7, 0.3, 1)]
    [InlineData(0.3, 0.3, 0)]       // already within half a second: nothing written
    public async Task Sync_Nanotag_WholeSecondsOnly_KeepsSubSecondOffset(double offset, double residual, int sets)
    {
        var rc = ReferenceClock.ForTest(0, 0.001);
        var dev = new FakeNanotag(rc) { Offset = offset };

        SyncResult r = await RtcSyncService.SyncAsync(dev, rc, deviceUtc: true, store: null);

        Assert.True(r.Ok, r.Message);
        Assert.Equal(0, dev.Shifts);
        Assert.Equal(sets, dev.Sets);
        Assert.Equal(residual, r.After!.OffsetSeconds, 2);
        Assert.Contains("cannot shift", r.Message);
    }

    [Theory]
    [InlineData((ushort)0x05, 5)]
    [InlineData((ushort)0x85, -5)]
    [InlineData((ushort)0x7F, 127)]
    [InlineData((ushort)0x00, 0)]
    public void FreqCorr_SignAndSteps(ushort reg, int steps) => Assert.Equal(steps, ClockCalibrationViewModel.FreqCorrSteps(reg));

    [Fact]
    public void RtcCalReply_NanotagLayout_Parses()
    {
        // message 78 layout, [15..16] = FREQCORR
        var b = new byte[23];
        b[0] = (byte)(RtcCalFlags.UnitCalibration | RtcCalFlags.TemperatureComp | RtcCalFlags.FromDisk);
        BitConverter.GetBytes(23400).CopyTo(b, 1);
        BitConverter.GetBytes((short)2150).CopyTo(b, 5);
        BitConverter.GetBytes((short)2210).CopyTo(b, 7);
        BitConverter.GetBytes((ushort)42).CopyTo(b, 9);
        BitConverter.GetBytes(-23100).CopyTo(b, 11);
        BitConverter.GetBytes((ushort)0x97).CopyTo(b, 15);
        BitConverter.GetBytes((ushort)34).CopyTo(b, 17);
        BitConverter.GetBytes((short)2500).CopyTo(b, 19);
        BitConverter.GetBytes((ushort)600).CopyTo(b, 21);
        RtcCalStatus st = RtcCalStatus.Parse(b)!;

        var vm = new ClockCalibrationViewModel();
        vm.ShowStatus(st, nanotag: true);
        Assert.Equal("-23.100 ppm (RTC FREQCORR -23 steps, dithered)", vm.CorrectionText);
        Assert.Equal("crystal +23.400 ppm at 21.5 °C (loaded from the tag's EEPROM at boot)", vm.CalibrationText);
        Assert.StartsWith("on: −34 ppb/°C² around 25.0 °C, every 600 s", vm.TempCompText);
    }

    [Fact]
    public void Describe_Nanotag_IsSupported()
    {
        var d = new LoggerDevice("COM_TEST", 115200, DeviceTypes.Nanotag, 0x1234);
        string t = ClockCalibrationViewModel.Describe(d, true);
        Assert.DoesNotContain("available for KOL", t);
        Assert.Contains("whole seconds only", t);
        Assert.Contains("Nanotag", ClockCalibrationViewModel.Describe(null, true));
    }
}
