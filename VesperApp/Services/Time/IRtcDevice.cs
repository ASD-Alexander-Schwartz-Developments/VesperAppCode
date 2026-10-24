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
        Task<bool> SetRtcAsync(DateTime deviceTime, int timeoutMs = 2000);
        Task<bool> ShiftAsync(int microseconds, int timeoutMs = 2000);
        Task<RtcCalStatus?> CalAsync(byte[] payload, int timeoutMs = 2000);
    }
}
