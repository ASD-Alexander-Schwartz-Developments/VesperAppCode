using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace VesperApp.Services
{
    /// <summary>
    /// Drives the native cross-platform <c>pluto-gnss</c> transmitter (libiio) as a
    /// subprocess — the same plugin/subprocess pattern VesperApp uses for the GNSS
    /// decoder (geotag). The binary streams a CW tone or a gps-sdr-sim I/Q file with a
    /// gapless cyclic buffer; <see cref="LoHz"/> carries the per-Pluto clock pre-comp
    /// (L1 + ~22.6 kHz for GPS into the Aclys front end).
    ///
    /// SAFETY: L1 is protected — transmit only into a shielded enclosure / attenuated path.
    /// </summary>
    public sealed class PlutoGnssTx : IDisposable
    {
        public const long L1Hz = 1_575_420_000;

        private readonly string _bin;
        private Process? _proc;

        public PlutoGnssTx(string? binaryPath = null)
        {
            _bin = binaryPath ?? Locate();
        }

        /// <summary>Resolve the pluto-gnss binary: PLUTO_GNSS_BIN, then alongside the
        /// app, then a bare name on PATH.</summary>
        public static string Locate()
        {
            string exe = OperatingSystem.IsWindows() ? "pluto-gnss.exe" : "pluto-gnss";
            string? env = Environment.GetEnvironmentVariable("PLUTO_GNSS_BIN");
            if (!string.IsNullOrWhiteSpace(env) && File.Exists(env)) return env;
            string? dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (dir != null)
            {
                string local = Path.Combine(dir, exe);
                if (File.Exists(local)) return local;
                string tools = Path.Combine(dir, "tools", exe);
                if (File.Exists(tools)) return tools;
            }
            return exe; // rely on PATH
        }

        public bool IsTransmitting => _proc is { HasExited: false };

        /// <summary>Start a CW tone at L1+offset (cyclic; runs until <see cref="Stop"/>).</summary>
        public void StartTone(double offsetHz, double gainDb, long loHz = L1Hz)
            => Start($"tone --offset {offsetHz:0} --lo {loHz} --gain {gainDb:0.###} --rate 2600000");

        /// <summary>Stream a gps-sdr-sim int16 I/Q file (cyclic) at the calibrated LO.</summary>
        public void StartFile(string iqFile, long loHz, double gainDb, long rateHz = 2_600_000)
            => Start($"tx \"{iqFile}\" --lo {loHz} --gain {gainDb:0.###} --rate {rateHz}");

        private void Start(string args)
        {
            Stop();
            var psi = new ProcessStartInfo
            {
                FileName = _bin,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            _proc = Process.Start(psi) ?? throw new InvalidOperationException($"failed to start {_bin}");
        }

        public void Stop()
        {
            if (_proc is { HasExited: false })
            {
                try { _proc.Kill(entireProcessTree: true); _proc.WaitForExit(2000); } catch { /* ignore */ }
            }
            _proc?.Dispose();
            _proc = null;
        }

        public void Dispose() => Stop();
    }
}
