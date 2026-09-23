using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using VesperApp.Models;

namespace VesperApp.ViewModels
{
    /// <summary>
    /// KOL USB operational modes on the device console (Idle ↔ Live Audio ↔ Sampling)
    /// and the shared "device goes away and comes back" choreography that the mode
    /// switch and the dockless firmware flash both need. Every firmware command is
    /// ACKed <em>before</em> the transition, so after the ACK the CDC port is expected
    /// to vanish; the same serial re-appearing is a reconnect, never an error.
    /// Contract: asd-interfaces <c>interfaces/boot-and-modes.md</c>.
    /// </summary>
    public partial class MainViewViewModel
    {
        private KolUsbMode? _kolUsbMode;
        private string _kolStatusText = string.Empty;

        /// <summary>Last mode read from the selected KOL (null until queried).</summary>
        public KolUsbMode? KolUsbMode
        {
            get => _kolUsbMode;
            private set
            {
                if (_kolUsbMode == value) return;
                _kolUsbMode = value;
                RunOnUi(RaiseKolProperties);
            }
        }

        /// <summary>The selected device is a KOL on its CDC console.</summary>
        public bool IsKolSelected => SelectedLoggerDevice?.IsKol == true;

        public string KolUsbModeText => KolUsbModeInfo.Label(_kolUsbMode);

        public bool IsKolLiveAudio => _kolUsbMode == Models.KolUsbMode.LiveAudio;

        public string KolLiveAudioButtonText => IsKolLiveAudio ? "Stop Live Audio" : "Live Audio";

        /// <summary>Disk-dependent actions (Format) are meaningless while a KOL is in
        /// Live Audio — the medium belongs to the firmware and no MSC is exposed.</summary>
        public bool CanUseDeviceDisk => IsDeviceConnected && !(IsKolSelected && !KolUsbModeInfo.HasDisk(_kolUsbMode));

        /// <summary>One-line narration under the device actions ("switching…", "device
        /// is now sampling…"). Empty hides the line.</summary>
        public string KolStatusText
        {
            get => _kolStatusText;
            private set
            {
                if (_kolStatusText == value) return;
                _kolStatusText = value;
                RunOnUi(() =>
                {
                    this.RaisePropertyChanged(nameof(KolStatusText));
                    this.RaisePropertyChanged(nameof(HasKolStatus));
                });
            }
        }

        public bool HasKolStatus => !string.IsNullOrEmpty(_kolStatusText);

        public ICommand? KolToggleLiveAudioCommand { get; private set; }
        public ICommand? KolStartSamplingCommand { get; private set; }

        private void InitKolCommands()
        {
            KolToggleLiveAudioCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                KolUsbMode target = IsKolLiveAudio ? Models.KolUsbMode.Idle : Models.KolUsbMode.LiveAudio;
                await SwitchKolUsbModeAsync(target);
            });

            KolStartSamplingCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (!await ConfirmAsync("Start sampling",
                        "Put the device into Sampling mode?",
                        "The device will disconnect from USB and run its recording schedule. "
                        + "To bring it back to Idle later, reconnect it or use the magnet."))
                    return;
                await SwitchKolUsbModeAsync(Models.KolUsbMode.Sampling);
            });
        }

        private void RaiseKolProperties()
        {
            this.RaisePropertyChanged(nameof(IsKolSelected));
            this.RaisePropertyChanged(nameof(KolUsbMode));
            this.RaisePropertyChanged(nameof(KolUsbModeText));
            this.RaisePropertyChanged(nameof(IsKolLiveAudio));
            this.RaisePropertyChanged(nameof(KolLiveAudioButtonText));
            this.RaisePropertyChanged(nameof(CanUseDeviceDisk));
        }

        private void OnSelectedLoggerDeviceChanged()
        {
            _kolUsbMode = SelectedLoggerDevice?.UsbMode;
            RaiseKolProperties();
        }

        /// <summary>Called after a device connects: KOL reports its mode on connect.</summary>
        internal async Task OnDeviceConnectedAsync()
        {
            LoggerDevice? dev = SelectedLoggerDevice;
            if (dev?.IsKol == true && dev.IsConnected)
            {
                await RefreshKolUsbModeAsync();
                if (KolUsbMode == Models.KolUsbMode.LiveAudio)
                    KolStatusText = "Live Audio: the microphones are streaming as a 4-channel USB microphone on this computer. No disk is exposed in this mode.";
                else
                    KolStatusText = string.Empty;
            }
            else
            {
                KolStatusText = string.Empty;
            }
            RaiseKolProperties();
        }

        internal async Task RefreshKolUsbModeAsync()
        {
            LoggerDevice? dev = SelectedLoggerDevice;
            if (dev?.IsKol != true || !dev.IsConnected) return;
            KolUsbModeState? state = await dev.GetUsbModeAsync();
            KolUsbMode = state?.Active;
        }

        /// <summary>
        /// Command a KOL mode switch and shepherd the device through it:
        /// Idle ↔ Live Audio re-enumerates (~1–2 s) and is auto-reconnected;
        /// Sampling detaches the device for good and is narrated.
        /// </summary>
        internal async Task SwitchKolUsbModeAsync(KolUsbMode target)
        {
            LoggerDevice? dev = SelectedLoggerDevice;
            if (dev?.IsKol != true || !dev.IsConnected) return;
            string serial = dev.SerialNumber ?? string.Empty;

            KolStatusText = target == Models.KolUsbMode.Sampling
                ? "Arming the device for sampling…"
                : $"Switching to {KolUsbModeInfo.Label(target)} — the device will re-enumerate…";

            bool ack = await dev.SetUsbModeAsync(target);
            if (!ack)
            {
                KolStatusText = "The device did not acknowledge the mode change. It may be running older firmware without USB modes.";
                await ShowInfoAsync("USB mode", KolStatusText);
                return;
            }

            // The device drops off USB right after the ACK — disconnect our side first so
            // the port loss is never reported as an error, and pause discovery meanwhile.
            await PrepareForKolDetachAsync();
            try
            {
                if (target == Models.KolUsbMode.Sampling)
                {
                    KolUsbMode = Models.KolUsbMode.Sampling;
                    KolStatusText = "Device is now sampling. It has disconnected from USB and will run its schedule. Reconnect it later, or use the magnet, to return to Idle.";
                    await RunOnUiAsync(() =>
                    {
                        LoggerDevices.Remove(dev);
                        SelectedLoggerDevice = null;
                    });
                    await ShowInfoAsync("Sampling", KolStatusText);
                    return;
                }

                LoggerDevice? back = await WaitForKolReturnAsync(serial, TimeSpan.FromSeconds(15));
                if (back is null)
                {
                    KolStatusText = "The device did not come back on USB within 15 s. Unplug and replug it, then press Connect Device.";
                    await ShowInfoAsync("USB mode", KolStatusText);
                    return;
                }

                await RefreshKolUsbModeAsync();
                if (KolUsbMode == target)
                {
                    KolStatusText = target == Models.KolUsbMode.LiveAudio
                        ? "Live Audio: the microphones are streaming as a 4-channel USB microphone on this computer (use any recorder / sound settings). No disk is exposed in this mode."
                        : "Idle: console and USB disk available.";
                }
                else
                {
                    KolStatusText = $"The device came back in {KolUsbModeText}, not {KolUsbModeInfo.Label(target)}.";
                }
            }
            finally
            {
                ResumeScanning();
            }
        }

        /// <summary>Stop discovery and drop our side of the CDC link before a commanded
        /// detach (mode switch, bootloader entry).</summary>
        internal async Task PrepareForKolDetachAsync()
        {
            _timer?.Stop();
            try
            {
                IsClosing = true;
                await _deviceUsbAdapter.DeviceDisconnect();
            }
            catch { }
            finally
            {
                IsClosing = false;
            }
        }

        internal void ResumeScanning()
        {
            if (_timer?.Enabled == false) _timer.Start();
        }

        /// <summary>
        /// Wait for the KOL with <paramref name="serial"/> to re-enumerate on a CDC port,
        /// put it (back) in the device list, select it and connect. Returns the
        /// connected device, or null on timeout.
        /// </summary>
        internal async Task<LoggerDevice?> WaitForKolReturnAsync(string serial, TimeSpan timeout)
        {
            // Give the host a moment to tear the old port down before probing.
            await Task.Delay(1500);

            LoggerDevice? found = await _deviceUsbAdapter.WaitForComportDeviceAsync(serial, timeout);
            if (found is null) return null;

            await RunOnUiAsync(() =>
            {
                LoggerDevice? existing = LoggerDevices.FirstOrDefault(d => d.Equals(found));
                if (existing is null)
                {
                    LoggerDevices.Add(found);
                }
                else if (!ReferenceEquals(existing, found))
                {
                    LoggerDevices[LoggerDevices.IndexOf(existing)] = found;
                }
                int index = LoggerDevices.IndexOf(found);
                if (SelectedLoggerDeviceModel != null && index >= 0)
                    SelectedLoggerDeviceModel.SelectedIndex = index;
                SelectedLoggerDevice = found;
            });

            bool connected = false;
            try { connected = await _deviceUsbAdapter.DeviceConnect(found); }
            catch { }
            if (!connected) return null;

            // Let the connection monitor observe the new link before callers query it.
            await Task.Delay(300);
            return found;
        }

        private static async Task<bool> ConfirmAsync(string title, string header, string message)
        {
            var box = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
            {
                ButtonDefinitions = ButtonEnum.YesNo,
                ContentTitle = title,
                ContentHeader = header,
                ContentMessage = message,
                Icon = MsBox.Avalonia.Enums.Icon.Question,
                WindowIcon = App.MainWindow?.Icon,
            });
            return await box.ShowWindowDialogAsync(App.MainWindow) == ButtonResult.Yes;
        }

        private static async Task ShowInfoAsync(string title, string message)
        {
            var box = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
            {
                ButtonDefinitions = ButtonEnum.Ok,
                ContentTitle = title,
                ContentMessage = message,
                Icon = MsBox.Avalonia.Enums.Icon.Info,
                WindowIcon = App.MainWindow?.Icon,
            });
            await box.ShowWindowDialogAsync(App.MainWindow);
        }

        private static void RunOnUi(Action action)
        {
            if (Dispatcher.UIThread.CheckAccess()) action();
            else Dispatcher.UIThread.Post(action);
        }

        private static Task RunOnUiAsync(Action action)
        {
            if (Dispatcher.UIThread.CheckAccess()) { action(); return Task.CompletedTask; }
            return Dispatcher.UIThread.InvokeAsync(action).GetTask();
        }
    }
}
