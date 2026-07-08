using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASDLibUSBWrapper;
using FirmwareUpdater.FileFormat;
using FirmwareUpdater.Memory;
using LibUsbDfu;

namespace VesperApp.Services
{
    /// <summary>Progress callback payload for a flash operation.</summary>
    public class FlashProgress
    {
        public int Percent { get; init; }
        public string Status { get; init; } = string.Empty;
    }

    /// <summary>
    /// Flashes STM32U585 products (Vesper / Kol / Pipistrelle) over USB DFU. The device
    /// is forced into the STM32 system bootloader via the docking-station GPIOs (BOOT0
    /// high + reset); it re-enumerates as ST 0x0483/0xDF11 and the in-repo DFU host
    /// (FirmwareUpdater + LibUsbDfu over libusb) writes the firmware. Supports .dfu
    /// (DfuSe), .hex (Intel HEX) and raw .bin (flashed at 0x08000000).
    /// </summary>
    public static class Stm32DfuFlasher
    {
        public const int StVid = 0x0483;
        public const int StDfuPid = 0xDF11;
        public const ulong FlashBaseAddress = 0x08000000;

        public static async Task<bool> FlashAsync(DockAdapter dock, string firmwarePath,
            IProgress<FlashProgress>? progress, CancellationToken ct = default)
        {
            if (dock == null || !dock.IsConnected)
                throw new InvalidOperationException("Connect the docking station first — STM32 flashing drives BOOT0/reset through the dock.");
            if (!File.Exists(firmwarePath))
                throw new FileNotFoundException("Firmware file not found.", firmwarePath);

            // 1. Force the STM32 into its ROM system-DFU bootloader via the dock GPIOs.
            progress?.Report(new FlashProgress { Percent = 0, Status = "Entering bootloader (BOOT0 + reset)…" });
            await dock.SetEnableDevice(true);   // ensure the docked device is powered
            await dock.SetBoot0Mode(true);      // BOOT0 high → enter system bootloader on reset
            await Task.Delay(50, ct);
            await dock.ResetDevice();           // pulse NRST
            await Task.Delay(800, ct);          // allow USB re-enumeration as DFU

            try
            {
                // 2-5. Blocking libusb work runs off the UI thread.
                return await Task.Run(() => FlashCore(firmwarePath, progress, ct), ct);
            }
            finally
            {
                // 6. ALWAYS drop BOOT0 and reset — also when the flash failed or was
                // cancelled. Leaving BOOT0 asserted would strand the device in the
                // bootloader (and re-enter it on every reset) until a power cycle.
                progress?.Report(new FlashProgress { Percent = 100, Status = "Resetting into application…" });
                try
                {
                    await dock.SetBoot0Mode(false);
                    await dock.ResetDevice();
                }
                catch { /* dock connection may be gone; nothing more we can do */ }
            }
        }

        private static bool FlashCore(string firmwarePath, IProgress<FlashProgress>? progress, CancellationToken ct)
        {
            using var context = new UsbContext();

            // Wait for the ST DFU device to appear. Re-enumeration normally takes ~1 s,
            // but the FIRST DFU attach on a Windows host can take considerably longer
            // while the driver binds — so give it a generous 20 s.
            LibUsbDfu.Device? dfu = null;
            Exception? lastOpenError = null;
            for (int attempt = 0; attempt < 80 && dfu == null; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                // Probe for an exact 0483:DF11 match ourselves before opening —
                // OpenFirst's VID-only fallback could otherwise latch onto the CDC
                // application device (0483:A4F4) when the bootloader never started.
                bool present;
                using (var probe = context.FindMultipleDevices(d => d.VendorId == StVid && d.ProductId == StDfuPid))
                    present = probe.Count > 0;

                if (present)
                {
                    try { dfu = LibUsbDfu.Device.OpenFirst(context, StVid, StDfuPid); break; }
                    catch (Exception ex) { lastOpenError = ex; }   // seen, not openable (yet)
                }

                Thread.Sleep(250);
            }

            if (dfu == null)
                throw new InvalidOperationException(BuildNoDfuMessage(context, lastOpenError));

            try
            {
                dfu.DownloadProgressChanged += (s, e) =>
                    progress?.Report(new FlashProgress { Percent = e.ProgressPercentage, Status = "Writing firmware…" });

                progress?.Report(new FlashProgress { Percent = 0, Status = "Writing firmware…" });

                // The U585 ROM bootloader programs 16-byte quad-words and SILENTLY
                // ignores writes at unaligned addresses (verified on hardware: a hex
                // segment at 0x08000238 left its whole region erased while every write
                // reported success) — so .hex/.bin images are merged/padded to quad-word
                // alignment first. ST-produced .dfu files are already aligned per target.
                string ext = Path.GetExtension(firmwarePath).ToLowerInvariant();
                switch (ext)
                {
                    case ".dfu":
                        dfu.DownloadFirmware(FirmwareUpdater.FileFormat.Dfu.ParseFile(firmwarePath));
                        break;
                    case ".hex":
                        dfu.DownloadFirmware(FlashAlignment.Normalize(IntelHex.ParseFile(firmwarePath)));
                        break;
                    case ".bin":
                        dfu.DownloadFirmware(FlashAlignment.Normalize(BinToMemory(firmwarePath, FlashBaseAddress)));
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported firmware type '{ext}'. Use .dfu, .hex or .bin.");
                }

                progress?.Report(new FlashProgress { Percent = 100, Status = "Finishing…" });
                // Leave DFU. Some ROM bootloaders stall the status phase on the leave
                // command; the dock-driven BOOT0-low reset that follows boots the new
                // firmware regardless, so a manifest hiccup is not a flash failure.
                try { dfu.Manifest(); } catch { }
                return true;
            }
            finally
            {
                try { if (dfu.IsOpen()) dfu.Close(); } catch { }
                dfu.Dispose();
            }
        }

        /// <summary>Build an actionable timeout message: distinguish "device never came
        /// back on USB", "device booted its application instead of the bootloader" and
        /// "DFU present but unopenable (driver)" — each has a different remedy.</summary>
        private static string BuildNoDfuMessage(UsbContext context, Exception? lastOpenError)
        {
            string seen;
            try
            {
                using var st = context.FindMultipleDevices(d => d.VendorId == StVid);
                seen = st.Count == 0
                    ? "No ST (0483:*) USB device is visible at all — the device did not re-enumerate. "
                      + "Check that it is seated in the dock (TOP marking aligned) and powered (Enable Device)."
                    : "Visible ST devices: " + string.Join(", ", st.Select(d => $"0483:{d.ProductId:X4}")) + ". "
                      + "If the application id (A4F4) is listed, the device booted its firmware instead of the "
                      + "bootloader — the BOOT0 line had no effect (check dock contact / device option bytes nSWBOOT0).";
            }
            catch { seen = "USB enumeration failed while diagnosing."; }

            string driverHint = lastOpenError != null
                ? $" A DFU device was detected but could not be opened ({lastOpenError.Message}) — on Windows, "
                  + "install the WinUSB driver for 'STM32 BOOTLOADER' (included with STM32CubeProgrammer)."
                : string.Empty;

            return $"No usable STM32 DFU device ({StVid:X4}:{StDfuPid:X4}) appeared after the BOOT0/reset sequence. {seen}{driverHint}";
        }

        private static RawMemory BinToMemory(string path, ulong baseAddress)
        {
            var mem = new RawMemory();
            mem.TryAddSegment(new Segment(baseAddress, File.ReadAllBytes(path)));
            return mem;
        }
    }
}
