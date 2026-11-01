using System;
using System.Threading.Tasks;
using VesperApp.Models;

namespace VesperApp.Services.Time
{
    /// <summary>
    /// The RTC operations the sync/calibration logic needs. Implemented by <see cref="LoggerRtcDevice"/>
    /// over a real console logger and by a simulator in the bench harness, so the timing maths can be
    /// verified without hardware.
    /// </summary>
    public interface IRtcDevice
    {
        /// <summary>Stable per-unit key for the calibration store (the device serial / UID).</summary>
        string UnitKey { get; }

        Task<RtcExchange?> TimeSyncAsync(uint seq, int timeoutMs = 1000);
        /// <summary>Set the clock to <paramref name="deviceTime"/>, kept in <paramref name="clockBase"/> (UTC / local),
        /// which loggers that support it store with the clock and stamp into their data.</summary>
        Task<bool> SetRtcAsync(DateTime deviceTime, DeviceClockBase clockBase, int timeoutMs = 2000);
        Task<bool> ShiftAsync(int microseconds, int timeoutMs = 2000);
        Task<RtcCalStatus?> CalAsync(byte[] payload, int timeoutMs = 2000);

        /// <summary>
        /// The unit can shift its RTC by a fraction of a second (message 77) and SET restarts its sub-second
        /// counter (KOL / VesperU5). False on the Nanotag: SET writes whole seconds and keeps the second boundary,
        /// so a sync leaves the sub-second offset, which is measured and kept.
        /// </summary>
        bool CanShift => true;

        /// <summary>The unit keeps its calibration itself across power loss (Nanotag EEPROM), so there is no
        /// disk copy to write.</summary>
        bool StoresCalibration => false;

    }
}
