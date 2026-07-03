using System;
using System.IO;
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

            // 2-5. Blocking libusb work runs off the UI thread.
            bool ok = await Task.Run(() => FlashCore(firmwarePath, progress, ct), ct);

            // 6. Drop BOOT0 and reset so the device boots the freshly-written application.
            progress?.Report(new FlashProgress { Percent = 100, Status = "Resetting into application…" });
            await dock.SetBoot0Mode(false);
            await dock.ResetDevice();
            return ok;
        }

        private static bool FlashCore(string firmwarePath, IProgress<FlashProgress>? progress, CancellationToken ct)
        {
            using var context = new UsbContext();

            // Wait for the ST DFU device to appear (re-enumeration takes a moment).
            LibUsbDfu.Device? dfu = null;
            for (int attempt = 0; attempt < 24 && dfu == null; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                try { dfu = LibUsbDfu.Device.OpenFirst(context, StVid, StDfuPid); }
                catch (ArgumentException) { Thread.Sleep(250); } // not enumerated yet
            }

            if (dfu == null)
                throw new InvalidOperationException(
                    $"No STM32 DFU device ({StVid:X4}:{StDfuPid:X4}) appeared. Check the dock connection and that the device entered the bootloader.");

            try
            {
                dfu.DownloadProgressChanged += (s, e) =>
                    progress?.Report(new FlashProgress { Percent = e.ProgressPercentage, Status = "Writing firmware…" });

                progress?.Report(new FlashProgress { Percent = 0, Status = "Writing firmware…" });

                string ext = Path.GetExtension(firmwarePath).ToLowerInvariant();
                switch (ext)
                {
                    case ".dfu":
                        dfu.DownloadFirmware(FirmwareUpdater.FileFormat.Dfu.ParseFile(firmwarePath));
                        break;
                    case ".hex":
                        dfu.DownloadFirmware(IntelHex.ParseFile(firmwarePath));
                        break;
                    case ".bin":
                        dfu.DownloadFirmware(BinToMemory(firmwarePath, FlashBaseAddress));
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported firmware type '{ext}'. Use .dfu, .hex or .bin.");
                }

                progress?.Report(new FlashProgress { Percent = 100, Status = "Finishing…" });
                dfu.Manifest(); // leave DFU and reset into the new firmware
                return true;
            }
            finally
            {
                try { if (dfu.IsOpen()) dfu.Close(); } catch { }
                dfu.Dispose();
            }
        }

        private static RawMemory BinToMemory(string path, ulong baseAddress)
        {
            var mem = new RawMemory();
            mem.TryAddSegment(new Segment(baseAddress, File.ReadAllBytes(path)));
            return mem;
        }
    }
}
