using System;
using System.IO;
using System.Threading.Tasks;
using VesperApp.Models;
using VesperApp.Services;

namespace VesperApp.ViewModels
{
    /// <summary>
    /// KOL firmware flashing without the docking station: the app commands the
    /// running firmware into the ST ROM DFU bootloader over its own CDC console
    /// (<c>VESPER_ENTER_BOOTLOADER</c>), then runs the same hardware-verified STM32
    /// DFU flasher the dock flow uses. Nothing persistent changes on bootloader
    /// entry, so any reset or power cycle returns the device to its application —
    /// there is no reachable stuck-in-DFU state.
    /// Contract: asd-interfaces <c>interfaces/boot-and-modes.md</c>.
    /// </summary>
    public partial class FirmwareUpgradesViewModel
    {
        private const int KolReturnTimeoutSeconds = 20;

        private async Task<bool> RunKolFlash(ReleaseEntry selected)
        {
            LoggerDevice? dev = _main?.SelectedLoggerDevice;
            if (dev is null || !dev.IsKol || !dev.IsConnected)
            {
                FlashStatus = "Connect the KOL over USB, select it in the device list and press Connect Device first. "
                            + "KOL flashes directly over its USB cable — no docking station is needed.";
                await ShowInfo("KOL firmware update", FlashStatus);
                return false;
            }
            if (feedService is null)
            {
                FlashStatus = "No update source is configured in this build.";
                return false;
            }

            if (!await ConfirmFlash(DeviceTypes.Kol, Path.GetFileName(selected.Asset!)))
                return false;

            string serial = dev.SerialNumber ?? string.Empty;
            string tmp = Path.Combine(Path.GetTempPath(), Path.GetFileName(selected.Asset!));
            bool detached = false;

            IsFlashing = true;
            FlashPercent = 0;
            try
            {
                FlashStatus = "Downloading firmware…";
                await feedService.DownloadAssetAsync(selected, tmp);

                FlashStatus = "Commanding the KOL into its bootloader…";
                bool ack = await dev.EnterBootloaderAsync();
                if (!ack)
                {
                    FlashStatus = "The KOL did not acknowledge the bootloader command. "
                                + "Its current firmware may predate commanded DFU entry — flash it once through the docking station instead.";
                    await ShowInfo("KOL firmware update", FlashStatus);
                    return false;
                }

                // The device leaves USB right after the ACK and returns as ST DFU
                // 0483:DF11; drop our CDC link first so the port loss is not an error.
                await _main!.PrepareForKolDetachAsync();
                detached = true;

                var progress = new Progress<FlashProgress>(p =>
                {
                    FlashPercent = p.Percent;
                    FlashStatus = p.Status;
                });

                await Stm32DfuFlasher.FlashDfuDeviceAsync(tmp, progress, viaDock: false);

                FlashStatus = "Firmware written — waiting for the KOL to restart…";
                LoggerDevice? back = await _main.WaitForKolReturnAsync(serial, TimeSpan.FromSeconds(KolReturnTimeoutSeconds));
                if (back is null)
                {
                    FlashStatus = $"Firmware written, but the KOL did not reappear within {KolReturnTimeoutSeconds} s. "
                                + "Unplug and replug its USB cable (or press reset) — it boots the new firmware on any reset.";
                    await ShowInfo("KOL firmware update", FlashStatus);
                    return true;
                }

                // Let the periodic status reply populate the reported firmware id.
                await back.GetInfo();
                await Task.Delay(750);
                string fw = string.IsNullOrEmpty(back.FwId) ? string.Empty : $" (firmware {back.FwId})";
                FlashStatus = $"KOL firmware updated — device is back in Idle mode{fw}.";
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("KOL flash failed: " + ex);
                FlashStatus = "Flash failed: " + ex.Message
                            + " If the device is still in bootloader mode, unplug and replug it to return to the previous firmware, then try again.";
                await ShowInfo("KOL firmware update failed", FlashStatus);
                return false;
            }
            finally
            {
                IsFlashing = false;
                if (detached) _main?.ResumeScanning();
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }
    }
}
