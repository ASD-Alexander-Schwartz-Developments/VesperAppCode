using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace VesperApp.Services
{
    public enum DfuDriverResult
    {
        /// <summary>The driver package is in the Windows driver store.</summary>
        Installed,
        /// <summary>The user refused the administrator prompt.</summary>
        Declined,
        Failed,
        /// <summary>Not Windows, or the driver files are not part of this build.</summary>
        NotSupported,
    }

    /// <summary>
    /// The ST "STM32 Bootloader" driver package shipped with the Windows builds (drivers/st-dfu). The firmware
    /// flash talks to the ROM bootloader (USB DFU, 0483:DF11) through libusb, which needs WinUSB bound to it;
    /// Windows does not do that by itself. The package is added to the driver store with pnputil, which needs
    /// administrator rights (one UAC prompt) - VesperApp itself is installed per user without them.
    /// It is installed at setup (Velopack after-install hook, in a process of its own: the hook must return
    /// within seconds, a UAC prompt may stay open for longer) and offered again before a flash when missing.
    /// </summary>
    public static class DfuDriverInstaller
    {
        /// <summary>Command-line switch: install the driver if it is missing, then exit (no window).</summary>
        public const string InstallArgument = "--install-dfu-driver";

        public const string InfName = "STM32Bootloader.inf";

        public static string InfPath => Path.Combine(AppContext.BaseDirectory, "drivers", "st-dfu", InfName);

        public static bool IsSupported => OperatingSystem.IsWindows() && File.Exists(InfPath);

        /// <summary>pnputil exists as a 64-bit program only: a 32-bit process on 64-bit Windows reaches it through Sysnative.</summary>
        internal static string PnpUtilPath(string windowsDir, bool is64BitOs, bool is64BitProcess) =>
            Path.Combine(windowsDir, is64BitOs && !is64BitProcess ? "Sysnative" : "System32", "pnputil.exe");

        internal static string InstallArguments(string infPath) => $"/add-driver \"{infPath}\" /install";

        /// <summary>The package in the output of "pnputil /enum-drivers". The labels are translated, the file name is not.</summary>
        internal static bool IsListed(string enumDriversOutput) =>
            enumDriversOutput.Contains(InfName, StringComparison.OrdinalIgnoreCase);

        /// <summary>0 = added; 259 = added, no device present to install it on; 3010 = added, restart requested.</summary>
        internal static bool IsSuccessExitCode(int exitCode) => exitCode is 0 or 259 or 3010;

        private static string PnpUtil => PnpUtilPath(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.Is64BitOperatingSystem, Environment.Is64BitProcess);

        /// <summary>Whether the package is in the driver store; null when that cannot be told.</summary>
        public static bool? IsInstalled()
        {
            if (!OperatingSystem.IsWindows()) return null;
            try
            {
                using var p = Process.Start(new ProcessStartInfo(PnpUtil, "/enum-drivers")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                });
                if (p is null) return null;
                Task<string> output = p.StandardOutput.ReadToEndAsync();
                if (!p.WaitForExit(15000))
                {
                    try { p.Kill(); } catch { }
                    return null;
                }
                return p.ExitCode == 0 ? IsListed(output.Result) : null;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("ST DFU driver: the driver store could not be read: " + ex.Message);
                return null;
            }
        }

        /// <summary>Add the package to the driver store. Blocks while Windows asks for administrator permission.</summary>
        public static DfuDriverResult Install()
        {
            if (!IsSupported) return DfuDriverResult.NotSupported;
            try
            {
                using var p = Process.Start(new ProcessStartInfo(PnpUtil, InstallArguments(InfPath))
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden,
                });
                if (p is null) return DfuDriverResult.Failed;
                p.WaitForExit();
                bool ok = IsSuccessExitCode(p.ExitCode) && IsInstalled() != false;
                Trace.TraceInformation($"ST DFU driver: pnputil exit code {p.ExitCode}, {(ok ? "installed" : "not installed")}.");
                return ok ? DfuDriverResult.Installed : DfuDriverResult.Failed;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223 /* ERROR_CANCELLED: the UAC prompt was refused */)
            {
                Trace.TraceInformation("ST DFU driver: the administrator prompt was refused.");
                return DfuDriverResult.Declined;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("ST DFU driver: install failed: " + ex.Message);
                return DfuDriverResult.Failed;
            }
        }

        public static Task<DfuDriverResult> InstallAsync() => Task.Run(Install);

        /// <summary>The <see cref="InstallArgument"/> run: install when missing. Returns the process exit code.</summary>
        public static int RunFromCommandLine()
        {
            if (!IsSupported) return 0;
            if (IsInstalled() != false) return 0;        // present, or unknown: no prompt without a reason
            return Install() == DfuDriverResult.Installed ? 0 : 1;
        }

        public static bool IsInstallRun(string[] args) => args.Contains(InstallArgument, StringComparer.OrdinalIgnoreCase);

        /// <summary>Start the <see cref="InstallArgument"/> run in a process of its own and return at once.</summary>
        public static void StartDetached()
        {
            if (!IsSupported || Environment.ProcessPath is not { } exe) return;
            try
            {
                Process.Start(new ProcessStartInfo(exe, InstallArgument) { UseShellExecute = false, CreateNoWindow = true })?.Dispose();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("ST DFU driver: the install could not be started: " + ex.Message);
            }
        }
    }
}
