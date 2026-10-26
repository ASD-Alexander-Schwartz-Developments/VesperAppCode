using System.Reactive;
using System.Reactive.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using VesperApp.Models;
using VesperApp.ViewModels;
using Xunit;
using static VesperApp.Models.ConfigurationJSON;

namespace VesperApp.Tests.ViewModels;

/// <summary>Configuration editor without a window: the schedule list (<see cref="ScheduleControlViewModel"/>)
/// and the general device settings of <see cref="ScheduleEditorViewModel"/> (config.json ↔ editor fields).
/// The driver lists are static and shared, so nothing here selects a device type.</summary>
public class ScheduleEditorTests
{
    private static Task Run(ICommand c) => ((ReactiveCommand<Unit, Unit>)c).Execute().ToTask();

    private static ConfigScheduleJSONItem At(int minute) =>
        new() { Alarm = new DateTime(2000, 1, 1, 0, minute, 0), Configuration = WorkingConfiguration.Config1 };

    // ---- schedule list ----

    [Fact]
    public async Task ApplyEntry_Dated_TakesDateAndTime_DropsSeconds_AndResetsTheEditor()
    {
        var vm = new ScheduleControlViewModel(Array.Empty<ConfigScheduleJSONItem>()) { SelectedScheduleType = ScheduleTypes.Dated };
        vm.IsAddingNewEntry = true;
        vm.SelectedDate = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);
        vm.SelectedTime = new TimeSpan(14, 30, 45);
        vm.SelectedConfiguration = WorkingConfiguration.Config2;

        await Run(vm.CommandApplyButton);

        ConfigScheduleJSONItem e = Assert.Single(vm.ScheduleEventsList);
        Assert.Equal(new DateTime(2026, 11, 3, 14, 30, 0), e.Alarm);
        Assert.Equal(WorkingConfiguration.Config2, e.Configuration);
        Assert.False(e.IsRelative);
        Assert.False(vm.IsAddingNewEntry);
        Assert.Null(vm.SelectedDate);
        Assert.Null(vm.SelectedTime);
        Assert.Equal(WorkingConfiguration.Off, vm.SelectedConfiguration);
    }

    [Fact]
    public async Task ApplyEntry_Daily_IsWrittenInYear2000()
    {
        var vm = new ScheduleControlViewModel(Array.Empty<ConfigScheduleJSONItem>()) { SelectedScheduleType = ScheduleTypes.Daily };
        vm.SelectedTime = new TimeSpan(6, 15, 0);
        vm.SelectedConfiguration = WorkingConfiguration.Config1;

        await Run(vm.CommandApplyButton);

        ConfigScheduleJSONItem e = Assert.Single(vm.ScheduleEventsList);
        Assert.Equal(2000, e.Alarm.Year);
        Assert.Equal(new TimeSpan(6, 15, 0), e.Alarm.TimeOfDay);
    }

    [Fact]
    public async Task ApplyEntry_Relative_IsADayOffsetPlusTime()
    {
        var vm = new ScheduleControlViewModel(Array.Empty<ConfigScheduleJSONItem>()) { SelectedScheduleType = ScheduleTypes.Relative };
        vm.SelectedDayOffset = 3;
        vm.SelectedTime = new TimeSpan(6, 15, 59);
        vm.SelectedConfiguration = WorkingConfiguration.Config1;

        await Run(vm.CommandApplyButton);

        ConfigScheduleJSONItem e = Assert.Single(vm.ScheduleEventsList);
        Assert.True(e.IsRelative);
        Assert.Equal("2000-00-03 06:15:00", e.Time);
        Assert.Equal(0, vm.SelectedDayOffset);
    }

    [Fact]
    public async Task ApplyEntry_ScheduleFull_AddsNothing()
    {
        var vm = new ScheduleControlViewModel(new[] { At(0), At(1) }) { MaxEntries = 2 };
        vm.SelectedScheduleType = ScheduleTypes.Continues;           // same type: the list stays
        Assert.Equal(2, vm.ScheduleEventsList.Count);
        vm.IsAddingNewEntry = true;

        await Run(vm.CommandApplyButton);

        Assert.Equal(2, vm.ScheduleEventsList.Count);
        Assert.False(vm.IsAddingNewEntry);
    }

    [Theory]
    [InlineData(ScheduleTypes.Dated, true, true, true, true)]
    [InlineData(ScheduleTypes.Daily, false, false, false, false)]
    [InlineData(ScheduleTypes.Relative, true, true, false, true)]
    public void ScheduleType_SetsTheDateEditorParts(ScheduleTypes type, bool dateEnabled, bool month, bool year, bool day)
    {
        var vm = new ScheduleControlViewModel(Array.Empty<ConfigScheduleJSONItem>()) { SelectedScheduleType = type };
        Assert.Equal(dateEnabled, vm.IsDateEnabled);
        Assert.Equal(month, vm.IsMonthVisible);
        Assert.Equal(year, vm.IsYearVisible);
        Assert.Equal(day, vm.IsDayVisible);
        Assert.Equal(type == ScheduleTypes.Relative, vm.IsRelativeSchedule);
    }

    [Fact]
    public void ScheduleType_Change_ClearsTheEntries()
    {
        var vm = new ScheduleControlViewModel(new[] { At(0), At(1) });
        vm.SelectedScheduleType = ScheduleTypes.Continues;
        Assert.Equal(2, vm.ScheduleEventsList.Count);
        vm.SelectedScheduleType = ScheduleTypes.Daily;
        Assert.Empty(vm.ScheduleEventsList);
        Assert.Equal("0 / 150", vm.EntryCountText);
    }

    [Fact]
    public void ApplyScheduleTypeToEntries_MarksEntriesRelativeOnlyForARelativeSchedule()
    {
        var vm = new ScheduleControlViewModel(Array.Empty<ConfigScheduleJSONItem>()) { SelectedScheduleType = ScheduleTypes.Relative };
        vm.ScheduleEventsList.Add(new ConfigScheduleJSONItem { Alarm = new DateTime(2000, 1, 5, 8, 0, 0) });
        vm.ApplyScheduleTypeToEntries();
        Assert.Equal("2000-00-05 08:00:00", vm.ScheduleEventsList[0].Time);   // day of month read as day offset

        var daily = new ScheduleControlViewModel(Array.Empty<ConfigScheduleJSONItem>()) { SelectedScheduleType = ScheduleTypes.Daily };
        daily.ScheduleEventsList.Add(ConfigScheduleJSONItem.Relative(2, new TimeSpan(9, 0, 0), WorkingConfiguration.Config1));
        daily.ApplyScheduleTypeToEntries();
        Assert.False(daily.ScheduleEventsList[0].IsRelative);
    }

    [Fact]
    public async Task UpDown_MoveTheSelectedEntry_AndStopAtTheEnds()
    {
        ConfigScheduleJSONItem a = At(0), b = At(1), c = At(2);
        var vm = new ScheduleControlViewModel(new[] { a, b, c }) { SelectedIndex = 2 };

        await Run(vm.CommandUpButton);
        Assert.Equal(new[] { a, c, b }, vm.ScheduleEventsList);
        Assert.Equal(1, vm.SelectedIndex);

        await Run(vm.CommandUpButton);
        await Run(vm.CommandUpButton);                                // already first
        Assert.Equal(new[] { c, a, b }, vm.ScheduleEventsList);
        Assert.Equal(0, vm.SelectedIndex);

        vm.SelectedIndex = 2;
        await Run(vm.CommandDownButton);                              // already last
        Assert.Equal(new[] { c, a, b }, vm.ScheduleEventsList);
    }

    [Fact]
    public void MaxEntries_NeverNegative()
    {
        var vm = new ScheduleControlViewModel(Array.Empty<ConfigScheduleJSONItem>()) { MaxEntries = -3 };
        Assert.Equal(0, vm.MaxEntries);
        Assert.False(vm.CanAddEntry);
        Assert.False(vm.TryAddEntry(At(0)));
    }

    // ---- general device settings ----

    private static ConfigurationJSON Config(Action<ConfigurationJSON> set)
    {
        var c = new ConfigurationJSON();
        set(c);
        return c;
    }

    [Fact]
    public async Task GeneralSettings_RoundTrip()
    {
        ConfigurationJSON src = Config(c =>
        {
            c.Name = "Kol";
            c.MinimumSupportedHardware = "4.1.0";
            c.IsMagnetOffEnabled = false;
            c.BatteryCapacity = 120;
            c.CDrift = 33066;
            c.TempComp = true;
            c.TempCompCurvature = 34;
            c.TempCompTurnover = 255;                                  // 25.5 °C
            c.TempCompPeriod = 600;
            c.PowerOn = new PowerOnTime { IsRelative = true, PowerOn = "01 02:03:00" };
        });
        var vm = new ScheduleEditorViewModel();
        await vm.ShowGeneralSettings(src);

        Assert.Equal("Kol", vm.DeviceName);
        Assert.Equal(33066m, vm.ClockDrift);
        Assert.True(vm.TempCompEnabled);
        Assert.Equal(25.5m, vm.TempCompTurnoverC);
        Assert.True(vm.IsPowerOnRelative);
        Assert.Equal("00 00:00:00", vm.PowerOnEditMask);

        var dst = new ConfigurationJSON();
        vm.ApplyGeneralSettings(dst);
        Assert.Equal("Kol", dst.Name);
        Assert.Equal("4.1.0", dst.MinimumSupportedHardware);
        Assert.False(dst.IsMagnetOffEnabled);
        Assert.Equal(120u, dst.BatteryCapacity);
        Assert.Equal(33066u, dst.CDrift);
        Assert.True(dst.TempComp);
        Assert.Equal(34u, dst.TempCompCurvature);
        Assert.Equal(255u, dst.TempCompTurnover);
        Assert.Equal(600u, dst.TempCompPeriod);
        Assert.True(dst.PowerOn.IsRelative);
        Assert.Equal("01 02:03:00", dst.PowerOn.PowerOn);
    }

    [Fact]
    public void GeneralSettings_TempCompOff_WritesNoTempCompKeys_EmptyPowerOnResets()
    {
        var vm = new ScheduleEditorViewModel
        {
            TempCompEnabled = false,
            TempCompCurvature = 34,
            TempCompTurnoverC = 25.04m,
            TempCompPeriod = 600,
            ClockDrift = null,
            PowerOnText = "",
        };
        var dst = Config(c => c.PowerOn = new PowerOnTime { IsRelative = true, PowerOn = "02 00:00:00" });
        vm.ApplyGeneralSettings(dst);

        Assert.Null(dst.TempComp);
        Assert.Null(dst.TempCompCurvature);
        Assert.Null(dst.TempCompTurnover);
        Assert.Null(dst.TempCompPeriod);
        Assert.Null(dst.CDrift);
        Assert.Equal("", dst.PowerOn.PowerOn ?? "");

        vm.TempCompEnabled = true;
        vm.TempCompCurvature = null;                                   // empty field: firmware default
        vm.ApplyGeneralSettings(dst);
        Assert.True(dst.TempComp);
        Assert.Null(dst.TempCompCurvature);
        Assert.Equal(250u, dst.TempCompTurnover);                      // 25.04 °C -> 250 tenths
    }

    [Fact]
    public void PowerOnMask_FollowsRelativeOrAbsolute()
    {
        var vm = new ScheduleEditorViewModel { IsPowerOnRelative = false };
        Assert.Equal("0000-00-00 00:00:00", vm.PowerOnEditMask);
        vm.IsPowerOnRelative = true;
        Assert.Equal("00 00:00:00", vm.PowerOnEditMask);
    }
}
