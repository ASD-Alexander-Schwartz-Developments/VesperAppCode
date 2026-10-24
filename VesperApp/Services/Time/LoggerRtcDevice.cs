using System;
using System.Threading.Tasks;
using VesperApp.Models;

namespace VesperApp.Services.Time
{
    /// <summary><see cref="IRtcDevice"/> over a console-attached <see cref="LoggerDevice"/>.</summary>
    public sealed class LoggerRtcDevice : IRtcDevice
    {
        private readonly LoggerDevice _dev;

        public LoggerRtcDevice(LoggerDevice dev) { _dev = dev; }

        public string UnitKey => string.IsNullOrWhiteSpace(_dev.SerialNumber) ? "unknown" : _dev.SerialNumber!.Trim();

        public Task<RtcExchange?> TimeSyncAsync(uint seq, int timeoutMs = 1000) => _dev.TimeSyncExchangeAsync(seq, timeoutMs);
        public Task<bool> SetRtcAsync(DateTime deviceTime, int timeoutMs = 2000) => _dev.SetRtcAsync(deviceTime, timeoutMs);
        public Task<bool> ShiftAsync(int microseconds, int timeoutMs = 2000) => _dev.RtcShiftAsync(microseconds, timeoutMs);
        public Task<RtcCalStatus?> CalAsync(byte[] payload, int timeoutMs = 2000) => _dev.RtcCalAsync(payload, timeoutMs);
    }
}
