using System;

namespace VesperApp.Models
{
    /// <summary>
    /// Device RTC time as transported by VESPER_TIME_SYNC: seconds since 2000-01-01 00:00:00 of the
    /// device's own calendar (no time zone) plus a 1/65536 s fraction. Kept as a double of seconds:
    /// ~8e8 s with sub-microsecond precision.
    /// </summary>
    public static class DeviceEpoch
    {
        public static readonly DateTime Epoch2000 = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        public static double ToSeconds(DateTime calendar) => (calendar - Epoch2000).TotalSeconds;

        public static DateTime FromSeconds(double seconds) =>
            DateTime.SpecifyKind(Epoch2000.AddTicks((long)Math.Round(seconds * TimeSpan.TicksPerSecond)), DateTimeKind.Unspecified);

        internal static double Read(byte[] b, int at) =>
            BitConverter.ToUInt32(b, at) + BitConverter.ToUInt16(b, at + 4) / 65536.0;
    }

    /// <summary>Status flags shared by the TIME_SYNC and RTC_CAL replies (rtc_cal.h RTCCAL_F_*).</summary>
    [Flags]
    public enum RtcCalFlags : byte
    {
        None = 0,
        UnitCalibration = 0x01,     // per-unit crystal calibration stored in the backup registers
        TemperatureComp = 0x02,     // config.json "tcomp" enabled
        LegacyDrift = 0x04,         // legacy config.json "cdrift" in use
        ShiftPending = 0x08,
        TemperaturePending = 0x10,
        TemperatureValid = 0x20,
        TemperatureFault = 0x40,    // ADC reference missing: the reading is impossible (VREFINT saturated)
    }

    /// <summary>One VESPER_TIME_SYNC exchange: host Stopwatch timestamps around the request (t1, t4)
    /// and the device RTC when it handled it (t2) and replied (t3).</summary>
    public sealed record RtcExchange(
        uint Seq, long HostSendTimestamp, long HostReceiveTimestamp,
        double DeviceReceiveSeconds, double DeviceReplySeconds,
        double? TemperatureC, int TemperatureAgeSeconds, int AppliedPpb, RtcCalFlags Flags)
    {
        public const int ReplyLength = 26;

        public static RtcExchange? Parse(byte[]? b, long t1, long t4)
        {
            if (b == null || b.Length < ReplyLength) return null;
            short t = BitConverter.ToInt16(b, 16);
            ushort age = BitConverter.ToUInt16(b, 18);
            return new RtcExchange(
                BitConverter.ToUInt32(b, 0), t1, t4,
                DeviceEpoch.Read(b, 4), DeviceEpoch.Read(b, 10),
                t == short.MinValue ? null : t / 100.0,
                age == 0xFFFF ? -1 : age,
                BitConverter.ToInt32(b, 20),
                (RtcCalFlags)b[24]);
        }
    }

    /// <summary>VESPER_RTC_CAL reply: stored per-unit calibration, temperature, applied correction.</summary>
    public sealed record RtcCalStatus(
        RtcCalFlags Flags, int StoredErrorPpb, double? ReferenceTemperatureC,
        double? TemperatureC, int TemperatureAgeSeconds, int AppliedPpb, ushort Calr,
        int CurvaturePpbPerC2, double TurnoverC, int PeriodSeconds)
    {
        public const int ReplyLength = 23;
        public const short TemperatureNone = short.MinValue;

        public bool HasUnitCalibration => (Flags & RtcCalFlags.UnitCalibration) != 0;
        public bool TemperatureCompensation => (Flags & RtcCalFlags.TemperatureComp) != 0;

        public static RtcCalStatus? Parse(byte[]? b)
        {
            if (b == null || b.Length < ReplyLength) return null;
            short tref = BitConverter.ToInt16(b, 5);
            short t = BitConverter.ToInt16(b, 7);
            ushort age = BitConverter.ToUInt16(b, 9);
            return new RtcCalStatus(
                (RtcCalFlags)b[0],
                BitConverter.ToInt32(b, 1),
                tref == TemperatureNone ? null : tref / 100.0,
                t == TemperatureNone ? null : t / 100.0,
                age == 0xFFFF ? -1 : age,
                BitConverter.ToInt32(b, 11),
                BitConverter.ToUInt16(b, 15),
                BitConverter.ToUInt16(b, 17),
                BitConverter.ToInt16(b, 19) / 100.0,
                BitConverter.ToUInt16(b, 21));
        }

        /// <summary>Request payload for op 1 (set): crystal error in ppb at the reference temperature.</summary>
        public static byte[] PackSet(int errorPpb, double? referenceC)
        {
            var p = new byte[7];
            p[0] = 1;
            BitConverter.GetBytes(errorPpb).CopyTo(p, 1);
            short tref = referenceC.HasValue ? (short)Math.Round(referenceC.Value * 100.0) : TemperatureNone;
            BitConverter.GetBytes(tref).CopyTo(p, 5);
            return p;
        }
    }
}
