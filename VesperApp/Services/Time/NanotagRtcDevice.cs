using System;
using System.Threading.Tasks;
using VesperApp.Models;

namespace VesperApp.Services.Time
{
    /// <summary>
    /// <see cref="IRtcDevice"/> over a Nanotag's USB vendor interface (firmware 1.37+, asd-interfaces
    /// interfaces/rtc-time-sync.md section Nanotag): TIME_SYNC 0x08, RTC_CAL 0x09, Set Time = SET_DATETIME 0x06
    /// (whole seconds, the second boundary is not moved). There is no sub-second shift.
    /// </summary>
    public sealed class NanotagRtcDevice : IRtcDevice
    {
        private readonly LoggerDevice _dev;

        public NanotagRtcDevice(LoggerDevice dev) { _dev = dev; }

        public string UnitKey => string.IsNullOrWhiteSpace(_dev.SerialNumber) ? "unknown" : _dev.SerialNumber!.Trim();

        public bool CanShift => false;
        public bool StoresCalibration => true;

        public Task<RtcExchange?> TimeSyncAsync(uint seq, int timeoutMs = 1000) => _dev.NanotagTimeSyncAsync(seq);
        public Task<bool> SetRtcAsync(DateTime deviceTime, int timeoutMs = 2000) => _dev.SetDateTime(deviceTime);
        public Task<bool> ShiftAsync(int microseconds, int timeoutMs = 2000) => Task.FromResult(false);
        public Task<RtcCalStatus?> CalAsync(byte[] payload, int timeoutMs = 2000) => _dev.NanotagRtcCalAsync(payload);
    }
}
