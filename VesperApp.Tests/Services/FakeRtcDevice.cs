using System.Diagnostics;
using VesperApp.Models;
using VesperApp.Services.Time;

namespace VesperApp.Tests.Services;

/// <summary>
/// A simulated console logger for the RTC sync / calibration code: its calendar runs <see cref="OffsetSeconds"/>
/// ahead of the reference (device in UTC), SET and SHIFT move it, RTC_CAL answers from <see cref="CalReply"/>.
/// Every request is logged in <see cref="Calls"/>.
/// </summary>
internal sealed class FakeRtcDevice : IRtcDevice
{
    private readonly ReferenceClock _rc;
    public double OffsetSeconds;
    public double OneWay = 0.0004, Processing = 0.0002;
    public RtcCalFlags ExchangeFlags = RtcCalFlags.UnitCalibration;
    public int AppliedPpb = -205000;
    public double? TemperatureC = 24.5;
    public bool Answers = true;
    public bool CanShiftValue = true, StoresCalibrationValue;

    /// <summary>RTC_CAL reply for a request payload (op in byte 0); null = no answer.</summary>
    public Func<byte[], RtcCalStatus?> CalReply = _ => null;

    public readonly List<string> Calls = new();
    public readonly List<byte[]> CalPayloads = new();

    public FakeRtcDevice(ReferenceClock rc, string unitKey = "SIM")
    {
        _rc = rc;
        UnitKey = unitKey;
    }

    public string UnitKey { get; }
    public bool CanShift => CanShiftValue;
    public bool StoresCalibration => StoresCalibrationValue;

    private static long Ticks(double s) => (long)(s * Stopwatch.Frequency);

    public Task<RtcExchange?> TimeSyncAsync(uint seq, int timeoutMs = 1000)
    {
        if (!Answers) return Task.FromResult<RtcExchange?>(null);
        long t1 = Stopwatch.GetTimestamp();
        double trueRx = RtcSyncService.DeviceSecondsOfTrue(_rc.TrueUtcAt(t1 + Ticks(OneWay)), true);
        long t4 = t1 + Ticks(2 * OneWay + Processing);
        double rx = trueRx + OffsetSeconds;
        return Task.FromResult<RtcExchange?>(new RtcExchange(seq, t1, t4, rx, rx + Processing, TemperatureC, 3, AppliedPpb, ExchangeFlags));
    }

    public Task<bool> SetRtcAsync(DateTime deviceTime, int timeoutMs = 2000)
    {
        Calls.Add("set");
        /* The request arrives one way later. KOL / Vesper restart the sub-second counter at deviceTime;
           a Nanotag (no shift) writes the whole seconds and keeps its sub-second phase. */
        double arriveTrue = RtcSyncService.DeviceSecondsOfTrue(_rc.TrueUtcNow(), true) + OneWay;
        double devNow = arriveTrue + OffsetSeconds;
        double keep = CanShiftValue ? 0.0 : devNow - Math.Floor(devNow);
        OffsetSeconds = DeviceEpoch.ToSeconds(deviceTime) + keep - arriveTrue;
        return Task.FromResult(true);
    }

    public Task<bool> ShiftAsync(int microseconds, int timeoutMs = 2000)
    {
        Calls.Add($"shift {microseconds}");
        OffsetSeconds += microseconds / 1e6;
        return Task.FromResult(true);
    }

    public Task<RtcCalStatus?> CalAsync(byte[] payload, int timeoutMs = 2000)
    {
        Calls.Add($"cal {payload[0]}");
        CalPayloads.Add(payload);
        return Task.FromResult(CalReply(payload));
    }

    /// <summary>An RTC_CAL status reply.</summary>
    public static RtcCalStatus Status(RtcCalFlags flags, int storedErrorPpb = 0, double? referenceC = null,
                                      double? temperatureC = 24.0, int appliedPpb = 0, ushort calr = 0,
                                      int curvature = 34, double turnoverC = 25.0, int period = 600) =>
        new(flags, storedErrorPpb, referenceC, temperatureC, 5, appliedPpb, calr, curvature, turnoverC, period);
}
