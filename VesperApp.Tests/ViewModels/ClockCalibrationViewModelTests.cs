using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using VesperApp.Models;
using VesperApp.Services.Time;
using VesperApp.Tests.Services;
using VesperApp.ViewModels;
using Xunit;

namespace VesperApp.Tests.ViewModels;

/// <summary>
/// Clock page workflow (Measure / Compute / Apply / Clear) on a simulated unit: a temp calibration store, a fixed
/// reference instead of NTP and a temp directory as the unit's drive. No window and no UI thread.
/// </summary>
public sealed class ClockCalibrationViewModelTests : IDisposable
{
    private static readonly DateTime T0 = new(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

    private readonly TempDir _dir = new();
    private readonly TempDir _disk = new();
    private readonly RtcCalibrationStore _store;
    private readonly ReferenceClock _rc = ReferenceClock.ForTest(0, 0.002);
    private readonly FakeRtcDevice _dev;
    private bool _connected = true;
    private bool _mounted = true;
    private int _referenceQueries;

    public ClockCalibrationViewModelTests()
    {
        _store = new RtcCalibrationStore(_dir.File("rtc.json"));
        _dev = new FakeRtcDevice(_rc, "47625051");
        File.WriteAllText(_disk.File("config.json"), """{"name":"KOL","cdrift":32999}""");
    }

    public void Dispose()
    {
        _dir.Dispose();
        _disk.Dispose();
    }

    private ClockCalibrationViewModel Vm(Func<Task<ReferenceClock>>? reference = null) =>
        new(null, _store, () => _connected ? _dev : null,
            reference ?? (() => { _referenceQueries++; return Task.FromResult(_rc); }),
            uid => _mounted && uid == "47625051" ? _disk.Path : null);

    private static Task Run(ICommand c) => ((ReactiveCommand<Unit, Unit>)c).Execute().ToTask();

    /// <summary>24 h of history with the clock running 1 ppm fast and no correction applied.</summary>
    private void History24h()
    {
        _store.Record("47625051", new OffsetMeasurement(T0, 0.010, 0, 0.0008, 0.0003, 30, 24.0, 0, 0), SyncPointKind.AfterSync, _rc);
        _store.Record("47625051", new OffsetMeasurement(T0.AddHours(24), 0.010 + 0.0864, 0, 0.0008, 0.0003, 30, 24.0, 0, 0), SyncPointKind.Measured, _rc);
    }

    private void UnitAccepts() => _dev.CalReply = p => p[0] switch
    {
        1 => FakeRtcDevice.Status(RtcCalFlags.UnitCalibration, BitConverter.ToInt32(p, 1), BitConverter.ToInt16(p, 5) / 100.0),
        2 => FakeRtcDevice.Status(RtcCalFlags.LegacyDrift),
        _ => null,
    };

    [Fact]
    public async Task Compute_WithoutEnoughData_SaysSo_AndApplyStaysDisabled()
    {
        var vm = Vm();
        await Run(vm.ComputeCommand);
        Assert.False(vm.HasEstimate);
        Assert.StartsWith("Not enough data: needs two measurements at least 1 h apart", vm.EstimateText);
        Assert.False(vm.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public async Task Compute_ShowsTheCrystalError()
    {
        History24h();
        var vm = Vm();
        await Run(vm.ComputeCommand);

        Assert.True(vm.HasEstimate);
        Assert.StartsWith("Crystal error +1.000 ppm (+86 ms/day uncorrected) ± ", vm.EstimateText);
        Assert.EndsWith("from 2 measurements over 24.0 h at 24.0 °C.", vm.EstimateText);
        Assert.True(vm.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public async Task Apply_WritesUnit_Store_AndDisk()
    {
        History24h();
        UnitAccepts();
        var vm = Vm();
        await Run(vm.ComputeCommand);
        await Run(vm.ApplyCommand);

        Assert.Equal(RtcCalStatus.PackSet(1000, 24.0), _dev.CalPayloads.Single());
        Assert.Equal(1000, _store.Get("47625051").Calibration!.ErrorPpb);
        Assert.Equal(1000, UnitDisk.Read(_disk.Path, "47625051")!.ErrorPpb);
        /* e(T0) = 1000 + 34 * (24 - 25)² = 1034 ppb -> correct by -1034 ppb: CALM 1 */
        Assert.Equal("""{"name":"KOL","cdrift":1}""", File.ReadAllText(_disk.File("config.json")));
        Assert.StartsWith("Calibration applied on the unit and stored here. Saved on the unit's disk", vm.StatusText);
        Assert.EndsWith("A new measurement period starts now.", vm.StatusText);
        Assert.Equal("crystal +1.000 ppm at 24.0 °C", vm.CalibrationText);
        Assert.False(vm.HasEstimate);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Apply_Refused_ChangesNothing()
    {
        History24h();
        _dev.CalReply = _ => FakeRtcDevice.Status(0);
        var vm = Vm();
        await Run(vm.ComputeCommand);
        await Run(vm.ApplyCommand);

        Assert.Equal("The device did not accept the calibration.", vm.StatusText);
        Assert.Null(_store.Get("47625051").Calibration);
        Assert.False(File.Exists(_disk.File(UnitDisk.CalibrationFile)));
        Assert.True(vm.HasEstimate);                                 // can be retried
    }

    [Fact]
    public async Task Apply_DiskNotMounted_KeepsItOnTheUnitAndSaysSo()
    {
        History24h();
        UnitAccepts();
        _mounted = false;
        var vm = Vm();
        await Run(vm.ComputeCommand);
        await Run(vm.ApplyCommand);

        Assert.Contains("The unit's disk was not found", vm.StatusText);
        Assert.NotNull(_store.Get("47625051").Calibration);
    }

    [Fact]
    public async Task Apply_UnitThatStoresItsOwnCalibration_WritesNoDisk()
    {
        History24h();
        UnitAccepts();
        _dev.StoresCalibrationValue = true;                          // Nanotag: EEPROM
        var vm = Vm();
        await Run(vm.ComputeCommand);
        await Run(vm.ApplyCommand);

        Assert.Contains("The tag keeps it in its EEPROM", vm.StatusText);
        Assert.False(File.Exists(_disk.File(UnitDisk.CalibrationFile)));
        Assert.Contains("32999", File.ReadAllText(_disk.File("config.json")));
    }

    [Fact]
    public async Task Clear_RemovesItFromUnitStoreAndDisk()
    {
        UnitAccepts();
        _store.SetCalibration("47625051", new StoredCalibration { ErrorPpb = 1000 });
        UnitDisk.Write(_disk.Path, "47625051", new CalibrationEstimate(1000, null, 20, 30, 2, 0, 1, null));
        var vm = Vm();
        await Run(vm.ClearCommand);

        Assert.Equal(new byte[] { 2 }, _dev.CalPayloads.Single());
        Assert.Null(_store.Get("47625051").Calibration);
        Assert.False(File.Exists(_disk.File(UnitDisk.CalibrationFile)));
        Assert.StartsWith("Calibration cleared on the unit and here. RTCCAL.TXT removed", vm.StatusText);
        Assert.Equal("none on the unit — the legacy config.json \"cdrift\" value applies", vm.CalibrationText);
    }

    [Fact]
    public async Task Clear_NoAnswer_KeepsTheStoredCopy()
    {
        _store.SetCalibration("47625051", new StoredCalibration { ErrorPpb = 1000 });
        var vm = Vm();
        await Run(vm.ClearCommand);
        Assert.Equal("The device did not answer.", vm.StatusText);
        Assert.NotNull(_store.Get("47625051").Calibration);
    }

    [Fact]
    public async Task Measure_RecordsAPoint_ShowsOffsetAndHistory_QueriesTheReferenceOnce()
    {
        _dev.OffsetSeconds = 0.25;
        var vm = Vm();
        await Run(vm.MeasureCommand);
        await Run(vm.MeasureCommand);

        Assert.StartsWith("+250.0 ms (device ahead); ± ", vm.OffsetText);
        Assert.Equal("Measured. ", vm.StatusText);
        Assert.Equal(2, _store.Get("47625051").Points.Count);
        Assert.Equal(2, vm.History.Count);
        Assert.Equal("measured", vm.History[0].Kind);
        Assert.Equal("+250.0", vm.History[0].Offset);
        Assert.Equal(1, _referenceQueries);                          // kept for 10 minutes
    }

    [Fact]
    public async Task Measure_UnitSilent_SaysSo()
    {
        _dev.Answers = false;
        var vm = Vm();
        await Run(vm.MeasureCommand);
        Assert.Equal("The device does not answer the time-sync command (firmware too old?).", vm.StatusText);
        Assert.Empty(_store.Get("47625051").Points);
    }

    [Fact]
    public async Task Refresh_UnitLostItsCalibration_SaysTheStoredValueIsReapplied()
    {
        _store.SetCalibration("47625051", new StoredCalibration { ErrorPpb = -203731 });
        _dev.CalReply = _ => FakeRtcDevice.Status(0);
        var vm = Vm();
        await Run(vm.RefreshCommand);
        Assert.Equal("The unit has lost its calibration (no RTCCAL.TXT on its disk and the RTC was reset); " +
                     "the stored value -203.731 ppm is re-applied on the next Sync.", vm.StatusText);
    }

    [Fact]
    public async Task Refresh_OldFirmware_SaysCalibrationIsNotSupported()
    {
        var vm = Vm();
        await Run(vm.RefreshCommand);
        Assert.StartsWith("The device does not support RTC calibration", vm.StatusText);
    }

    [Fact]
    public async Task NoDevice_CommandsExplainWhatToSelect()
    {
        _connected = false;
        var vm = Vm();
        await Run(vm.MeasureCommand);
        Assert.Equal(vm.DeviceText, vm.StatusText);
        Assert.StartsWith("Select a connected KOL", vm.StatusText);
    }

    [Fact]
    public async Task AFailingStep_IsReported_AndTheCommandsAreFreedAgain()
    {
        var vm = Vm(() => throw new InvalidOperationException("no network"));
        await Run(vm.SyncCommand);
        Assert.Equal("Failed: no network", vm.StatusText);
        Assert.False(vm.IsBusy);
        Assert.True(vm.SyncCommand.CanExecute(null));
    }

    [Fact]
    public void ShowStatus_ConsoleLoggerTexts()
    {
        var vm = Vm();
        vm.ShowStatus(FakeRtcDevice.Status(RtcCalFlags.UnitCalibration | RtcCalFlags.FromDisk | RtcCalFlags.TemperatureComp,
                                           -203731, 23.46, 24.0, appliedPpb: -204000), nanotag: false);
        Assert.Equal("crystal -203.731 ppm at 23.5 °C (loaded from RTCCAL.TXT on the unit's disk)", vm.CalibrationText);
        Assert.Equal("-204.000 ppm (RTC smooth calibration, time-averaged)", vm.CorrectionText);
        Assert.Equal("24.0 °C (internal sensor, 5 s ago)", vm.TemperatureText);
        Assert.StartsWith("on: −34 ppb/°C² around 25.0 °C, every 600 s", vm.TempCompText);

        vm.ShowStatus(FakeRtcDevice.Status(RtcCalFlags.TemperatureFault), nanotag: false);
        Assert.StartsWith("unavailable: the unit's ADC", vm.TemperatureText);
        Assert.Equal("none on the unit — no correction", vm.CalibrationText);
        Assert.StartsWith("off ", vm.TempCompText);
    }

    [Fact]
    public void ShowStatus_NanotagTexts()
    {
        var vm = Vm();
        vm.ShowStatus(FakeRtcDevice.Status(RtcCalFlags.UnitCalibration | RtcCalFlags.FromDisk, 12000, null, null, appliedPpb: 12000, calr: 0x0C),
                      nanotag: true);
        Assert.Equal("crystal +12.000 ppm at unknown temperature (loaded from the tag's EEPROM at boot)", vm.CalibrationText);
        Assert.Equal("+12.000 ppm (RTC FREQCORR +12 steps, dithered)", vm.CorrectionText);
        Assert.Equal("not measured yet", vm.TemperatureText);
    }

    [Theory]
    [InlineData((ushort)0x00, 0)]
    [InlineData((ushort)0x0C, 12)]
    [InlineData((ushort)0x8C, -12)]
    [InlineData((ushort)0xFF, -127)]
    public void FreqCorrSteps_SignAndMagnitude(ushort reg, int steps) => Assert.Equal(steps, ClockCalibrationViewModel.FreqCorrSteps(reg));

    [Fact]
    public void Describe_SelectedDevice()
    {
        Assert.StartsWith("Select a connected", ClockCalibrationViewModel.Describe(null, true));
        var kol = new LoggerDevice("COM_TEST", 115200, DeviceTypes.Kol, 0x4762505A, new Version(1, 8));
        Assert.Equal("Kol 4762505A — firmware 1.8 — device clock in UTC (press Connect Device first)",
                     ClockCalibrationViewModel.Describe(kol, deviceUtc: true));
        var tag = new LoggerDevice("COM_TEST", 115200, DeviceTypes.Nanotag, 0x1234, new Version(1, 36));
        string t = ClockCalibrationViewModel.Describe(tag, deviceUtc: false);
        Assert.Contains("device clock in local time — needs Nanotag firmware 1.37 or later", t);
        Assert.Contains("(sets whole seconds only", t);
    }
}
