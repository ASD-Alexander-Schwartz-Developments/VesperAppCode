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
    /// USB operational modes on the device console (Idle ↔ Live Audio ↔ Sampling) - KOL, and Vesper /
    /// Pipistrelle with new enough firmware (<see cref="LoggerDevice.SupportsUsbModes"/>) -
    /// and the shared "device goes away and comes back" choreography that the mode
    /// switch and the dockless firmware flash both need. Every firmware command is
    /// ACKed <em>before</em> the transition, so after the ACK the CDC port is expected
    /// to vanish; the same serial re-appearing is a reconnect, never an error.
    /// Contract: asd-interfaces <c>interfaces/boot-and-modes.md</c>.
    /// </summary>
    public partial class MainViewViewModel
    {
        private KolUsbMode? _kolUsbMode;
        private KolUsbModeState? _kolState;
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

        /// <summary>The selected unit has USB modes (KOL, or Vesper / Pipistrelle firmware 1.22+).</summary>
        public bool IsUsbModesSelected => SelectedLoggerDevice?.SupportsUsbModes == true;

        public string UsbModeToolTip
        {
            get
            {
                DeviceTypes? t = SelectedLoggerDevice?.DeviceType;
                if (IsKolStatesSelected)
                    return $"KOL operating state. Idle: console + USB disk (while connected to a computer). Live Audio: {MicsPhrase(t)} as a standard USB microphone (no disk). Recording: the KOL leaves USB and records according to config.json. Unplugged and not recording it sleeps; plugging it into a computer wakes it into Idle.";
                return $"{DeviceCapabilities.DisplayName(t)} USB mode. Idle: console + USB disk. Live Audio: {MicsPhrase(t)} as a standard USB microphone (no disk). Sampling: the device leaves USB and runs its schedule.";
            }
        }

        /// <summary>The selected unit is a KOL running the operating states (Sleep / Idle /
        /// Live Audio / Recording, no magnet): its GET_USB_MODE reply carries status flags.</summary>
        public bool IsKolStatesSelected => IsKolSelected && _kolState?.HasStatus == true;

        /// <summary>Sleep / Arm Sleep apply to units that sleep and arm on command; a KOL with the
        /// operating states sleeps by itself when unplugged and records through Start Recording.</summary>
        public bool ShowSleepCommands => !IsKolStatesSelected;

        public string StartRecordingButtonText => IsKolSelected ? "Start Recording" : "Start Sampling";

        public string StartRecordingToolTip => IsKolStatesSelected
            ? "Check the card, config.json, sensors, clock and free space, then let the KOL leave USB and record according to config.json. To stop recording, unplug it and connect it to a computer."
            : IsKolSelected
                ? "Arm the KOL and start its recording schedule. It disconnects from USB."
                : "Arm the device and start its recording schedule. It disconnects from USB; reconnect it or use the magnet to return to Idle.";

        private KolStatusFlags KolFlags => _kolState?.Flags ?? KolStatusFlags.None;

        public bool IsKolReady => _kolState?.Reason == KolRecordReason.Ok;

        /// <summary>Readiness pill: "Ready to record" or why not.</summary>
        public string KolReadinessText => _kolState?.Reason is { } r ? KolRecordReasonInfo.Short(r) : string.Empty;

        public string KolReadinessToolTip
        {
            get
            {
                if (_kolState?.Flags is not { } f) return string.Empty;
                static string Yn(bool b, string yes, string no) => b ? yes : no;
                string card = !f.HasFlag(KolStatusFlags.CardInserted) ? "no card"
                            : f.HasFlag(KolStatusFlags.CardMounted) ? "inserted, mounted" : "inserted, not readable";
                string lines =
                    $"SD card: {card}\n" +
                    $"config.json: {Yn(f.HasFlag(KolStatusFlags.ConfigOk), "loaded", "missing or not valid")}\n" +
                    $"Clock: {Yn(f.HasFlag(KolStatusFlags.RtcSet), "set", "not set")}\n" +
                    $"Sleeps when unplugged: {Yn(f.HasFlag(KolStatusFlags.VbusSense), "yes", "not yet (needs one USB plug-in; boards without the VBUS divider fix never do)")}";
                string advice = _kolState.Reason is { } r ? KolRecordReasonInfo.Advice(r) : string.Empty;
                return string.IsNullOrEmpty(advice) ? lines : lines + "\n\n" + advice;
            }
        }

        /// <summary>"the 4 microphones stream" / "the microphone streams".</summary>
        private static string MicsPhrase(DeviceTypes? t) =>
            DeviceCapabilities.MicCount(t) > 1 ? $"the {DeviceCapabilities.MicCount(t)} microphones stream" : "the microphone streams";

        /// <summary>Narration while a unit is in Live Audio.</summary>
        private static string LiveAudioText(LoggerDevice dev, bool recorderHint)
        {
            int ch = DeviceCapabilities.LiveAudioChannels(dev.DeviceType);
            string what = ch > 1 ? $"the microphones are streaming as a {ch}-channel USB microphone" : "the microphone is streaming as a USB microphone";
            return $"Live Audio: {what} on this computer{(recorderHint ? " (use any recorder / sound settings)" : "")}. No disk is exposed in this mode.";
        }

        public string KolUsbModeText => KolUsbModeInfo.Label(_kolUsbMode);

        public bool IsKolLiveAudio => _kolUsbMode == Models.KolUsbMode.LiveAudio;

        public string KolLiveAudioButtonText => IsKolLiveAudio ? "Stop Live Audio" : "Live Audio";

        /// <summary>Disk-dependent actions (Format) are meaningless while a KOL is in
        /// Live Audio — the medium belongs to the firmware and no MSC is exposed — or when
        /// a KOL reports no readable card.</summary>
        public bool CanUseDeviceDisk => IsDeviceConnected
            && !(IsUsbModesSelected && !KolUsbModeInfo.HasDisk(_kolUsbMode))
            && !(IsKolStatesSelected && !KolFlags.HasFlag(KolStatusFlags.CardMounted));

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
        public ICommand? KolCheckCommand { get; private set; }

        private void InitKolCommands()
        {
            KolToggleLiveAudioCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                KolUsbMode target = IsKolLiveAudio ? Models.KolUsbMode.Idle : Models.KolUsbMode.LiveAudio;
                await SwitchKolUsbModeAsync(target);
            });

            KolStartSamplingCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (IsKolStatesSelected)
                {
                    await StartKolRecordingAsync();
                    return;
                }
                if (!await ConfirmAsync("Start sampling",
                        "Put the device into Sampling mode?",
                        "The device will disconnect from USB and run its recording schedule. "
                        + "To bring it back to Idle later, reconnect it or use the magnet."))
                    return;
                await SwitchKolUsbModeAsync(Models.KolUsbMode.Sampling);
            });

            KolCheckCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                await RefreshKolUsbModeAsync(rereadConfig: true);
                KolStatusText = KolProblemText();
            });
        }

        /// <summary>One line for the status caption when a KOL cannot record (empty when it can,
        /// or when the unit does not report its status).</summary>
        private string KolProblemText()
        {
            if (!IsKolStatesSelected || _kolState?.Reason is not { } r || r == KolRecordReason.Ok) return string.Empty;
            return $"{KolRecordReasonInfo.Short(r)}: {KolRecordReasonInfo.Advice(r)}";
        }

        /// <summary>
        /// Start standalone recording on a KOL with the operating states: re-read config.json
        /// and check (offering to set the clock when that is all that is missing), confirm, then
        /// SET_USB_MODE(1). The firmware repeats the check and NACKs with the reason, so a
        /// refusal is always explained.
        /// </summary>
        private async Task StartKolRecordingAsync()
        {
            LoggerDevice? dev = SelectedLoggerDevice;
            if (dev?.SupportsUsbModes != true || !dev.IsConnected) return;

            KolStatusText = "Checking the card, config.json and clock…";
            await RefreshKolUsbModeAsync(rereadConfig: true);
            KolRecordReason? reason = _kolState?.Reason;

            if (reason == KolRecordReason.NoClock)
            {
                if (!await ConfirmAsync("Start recording", "The KOL's clock is not set.",
                        "Recordings need the right time. Set the clock now (network time) and continue?"))
                {
                    KolStatusText = KolProblemText();
                    return;
                }
                await SetDeviceClockAsync(dev);
                await RefreshKolUsbModeAsync();
                reason = _kolState?.Reason;
            }

            if (reason is { } r && r != KolRecordReason.Ok)
            {
                KolStatusText = KolProblemText();
                await ShowInfoAsync("Start recording", $"{KolRecordReasonInfo.Short(r)}.\n\n{KolRecordReasonInfo.Advice(r)}");
                return;
            }

            if (!await ConfirmAsync("Start recording", "Start recording now?",
                    "The KOL will disconnect from USB and record according to config.json on its card, "
                    + "sleeping between recording periods.\n\n"
                    + "It keeps recording on its battery. To stop recording, unplug it and connect it to a "
                    + "computer: it returns to Idle within about 5 s. A charger or power bank does not stop it."))
            {
                KolStatusText = string.Empty;
                return;
            }

            KolStatusText = "Starting the recording…";
            KolRecordResult res = await dev.StartRecordingAsync();
            if (!res.Started)
            {
                KolStatusText = res.Reason is { } why
                    ? $"{KolRecordReasonInfo.Short(why)}: {KolRecordReasonInfo.Advice(why)}"
                    : res.Answered ? "The KOL refused to start recording." : "The KOL did not answer. Check the connection and try again.";
                await RefreshKolUsbModeAsync();
                await ShowInfoAsync("Start recording", KolStatusText);
                return;
            }

            await PrepareForKolDetachAsync();
            try
            {
                KolUsbMode = Models.KolUsbMode.Sampling;
                KolStatusText = "Recording. The KOL has left USB (LED: 3 blinks, then off) and records according to config.json. "
                              + "To stop, unplug it and connect it to a computer.";
                await RunOnUiAsync(() =>
                {
                    LoggerDevices.Remove(dev);
                    SelectedLoggerDevice = null;
                });
                await ShowInfoAsync("Recording", KolStatusText);
            }
            finally
            {
                ResumeScanning();
            }
        }

        private void RaiseKolProperties()
        {
            this.RaisePropertyChanged(nameof(IsKolSelected));
            this.RaisePropertyChanged(nameof(IsUsbModesSelected));
            this.RaisePropertyChanged(nameof(UsbModeToolTip));
            this.RaisePropertyChanged(nameof(KolUsbMode));
            this.RaisePropertyChanged(nameof(KolUsbModeText));
            this.RaisePropertyChanged(nameof(IsKolLiveAudio));
            this.RaisePropertyChanged(nameof(KolLiveAudioButtonText));
            this.RaisePropertyChanged(nameof(CanUseDeviceDisk));
            this.RaisePropertyChanged(nameof(IsKolStatesSelected));
            this.RaisePropertyChanged(nameof(ShowSleepCommands));
            this.RaisePropertyChanged(nameof(StartRecordingButtonText));
            this.RaisePropertyChanged(nameof(StartRecordingToolTip));
            this.RaisePropertyChanged(nameof(IsKolReady));
            this.RaisePropertyChanged(nameof(KolReadinessText));
            this.RaisePropertyChanged(nameof(KolReadinessToolTip));
        }

        private void OnSelectedLoggerDeviceChanged()
        {
            _kolUsbMode = SelectedLoggerDevice?.UsbMode;
            _kolState = SelectedLoggerDevice?.UsbState;
            RaiseKolProperties();
        }

        /// <summary>Called after a device connects: KOL reports its mode on connect.</summary>
        internal async Task OnDeviceConnectedAsync()
        {
            LoggerDevice? dev = SelectedLoggerDevice;
            if (dev?.SupportsUsbModes == true && dev.IsConnected)
            {
                await RefreshKolUsbModeAsync(rereadConfig: true);
                if (KolUsbMode == Models.KolUsbMode.LiveAudio)
                    KolStatusText = LiveAudioText(dev, recorderHint: false);
                else
                    KolStatusText = KolProblemText();
            }
            else
            {
                KolStatusText = string.Empty;
            }
            RaiseKolProperties();
        }

        /// <param name="rereadConfig">KOL: have the firmware read config.json from the card again
        /// first (the user may have just copied one onto the USB disk).</param>
        internal async Task RefreshKolUsbModeAsync(bool rereadConfig = false)
        {
            LoggerDevice? dev = SelectedLoggerDevice;
            if (dev?.SupportsUsbModes != true || !dev.IsConnected) return;
            KolUsbModeState? state = await dev.GetUsbModeAsync(rereadConfig: rereadConfig);
            _kolState = state;
            KolUsbMode = state?.Active;
            RunOnUi(RaiseKolProperties);
        }

        /// <summary>
        /// Command a KOL mode switch and shepherd the device through it:
        /// Idle ↔ Live Audio re-enumerates (~1–2 s) and is auto-reconnected;
        /// Sampling detaches the device for good and is narrated.
        /// </summary>
        internal async Task SwitchKolUsbModeAsync(KolUsbMode target)
        {
            LoggerDevice? dev = SelectedLoggerDevice;
            if (dev?.SupportsUsbModes != true || !dev.IsConnected) return;
            string serial = dev.SerialNumber ?? string.Empty;

            KolStatusText = target == Models.KolUsbMode.Sampling
                ? "Arming the device for sampling…"
                : $"Switching to {KolUsbModeInfo.Label(target)} — the device will re-enumerate…";

            bool ack = await dev.SetUsbModeAsync(target);
            if (!ack)
            {
                KolStatusText = "The device refused or did not acknowledge the mode change. It may be running older firmware without USB modes.";
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
                        ? LiveAudioText(back, recorderHint: true)
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
                // Devices are equal only on serial AND port, but a re-enumerated KOL may come back
                // on another COM port (Live Audio is a different composite). The serial is the unit:
                // replace its old row, and drop any further stale ones - the periodic scan that would
                // remove them does not run while a device is connected.
                LoggerDevice? existing = LoggerDevices.FirstOrDefault(d => d.Equals(found))
                    ?? LoggerDevices.FirstOrDefault(d => d.IsComportDevice && SameSerial(d, found));
                if (existing is null)
                {
                    LoggerDevices.Add(found);
                }
                else if (!ReferenceEquals(existing, found))
                {
                    LoggerDevices[LoggerDevices.IndexOf(existing)] = found;
                }
                foreach (LoggerDevice stale in LoggerDevices
                             .Where(d => !ReferenceEquals(d, found) && d.IsComportDevice && SameSerial(d, found))
                             .ToList())
                    LoggerDevices.Remove(stale);
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

        private static bool SameSerial(LoggerDevice a, LoggerDevice b) =>
            !string.IsNullOrEmpty(a.SerialNumber)
            && string.Equals(a.SerialNumber, b.SerialNumber, StringComparison.OrdinalIgnoreCase);

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
