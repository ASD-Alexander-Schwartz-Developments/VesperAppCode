using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;
using System.Reactive.Linq;
using System.Windows.Input;
using VesperApp.Views;
using VesperApp.Services;
using VesperApp.Services.Time;
using VesperApp.Models;
using System.ComponentModel;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using Avalonia.Controls.Selection;
using System.Threading.Tasks;
using System.Text.Json;
using System.Diagnostics;
using Avalonia.Controls;
using System.IO;
using System.Reactive;
using Avalonia;
using System.Linq;
using Avalonia.Platform.Storage;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using System.Collections;
using System.Globalization;
using Microsoft.Extensions.Logging;
using MsBox.Avalonia.Base;
using VesperApp.Controls;
using MsBox.Avalonia.Models;
using FluentAvalonia.UI.Controls;
using static VesperApp.Models.ConfigurationJSON;






namespace VesperApp.ViewModels
{
    public partial class MainViewViewModel : ViewModelBase
    {
        private DockAdapter _globalDockAdapter;

        /// <summary>The shared docking-station adapter — firmware flashing uses it to drive BOOT0/reset.</summary>
        public DockAdapter GlobalDock => _globalDockAdapter;
        private DeviceUsbAdapter _deviceUsbAdapter;

        public bool IsConnected
        {
            get => _isConnected;
            set => this.RaiseAndSetIfChanged(ref _isConnected, value);
        }

        private bool _isConnected = false;


        public bool IsDeviceConnected
        {
            get => _isDeviceConnected;
            set
            {
                this.RaiseAndSetIfChanged(ref _isDeviceConnected, value);
                this.RaisePropertyChanged(nameof(IsDeviceIdle));
            }
        }

        private bool _isDeviceConnected = false;

        /// <summary>A Nanotag download is reading the tag: Format, Arm, Upload, Set Time, Upgrade and a second
        /// Download would talk to the same tag mid-transfer (Format would erase what is being read).</summary>
        public bool IsNanotagDownloading
        {
            get => _isNanotagDownloading;
            private set
            {
                this.RaiseAndSetIfChanged(ref _isNanotagDownloading, value);
                this.RaisePropertyChanged(nameof(IsDeviceIdle));
                this.RaisePropertyChanged(nameof(CanUseDeviceDisk));
                this.RaisePropertyChanged(nameof(CanFormatDevice));
            }
        }
        private bool _isNanotagDownloading;

        /// <summary>Device connected and not busy with a download: enables the device actions.</summary>
        public bool IsDeviceIdle => IsDeviceConnected && !IsNanotagDownloading;

        /// <summary>
        /// Whether the Logger Devices console (list + device actions) is shown. Collapsed, the
        /// working area (editor, Help, …) gets the full height — for offline work the console is
        /// just dead space. Persisted across sessions; connecting a dock re-expands it because
        /// that's the moment the console becomes relevant.
        /// </summary>
        public bool IsDeviceConsoleExpanded
        {
            get => _isDeviceConsoleExpanded;
            set
            {
                if (_isDeviceConsoleExpanded == value) return;
                this.RaiseAndSetIfChanged(ref _isDeviceConsoleExpanded, value);

                var cfg = SettingsService.Current;
                if (cfg.Ui.DeviceConsoleExpanded != value)
                {
                    cfg.Ui.DeviceConsoleExpanded = value;
                    SettingsService.Instance.Save();
                }
            }
        }

        private bool _isDeviceConsoleExpanded = true;

        public ICommand ToggleDeviceConsoleCommand { get; }


        public bool IsDeviceEnabled
        {
            get => _isDeviceEnabled;
            set => this.RaiseAndSetIfChanged(ref _isDeviceEnabled, value);
        }

        private bool _isDeviceEnabled = false;

        public bool IsDeviceBoot0Mode
        {
            get => _isDeviceBoot0Mode;
            set => this.RaiseAndSetIfChanged(ref _isDeviceBoot0Mode, value);
        }

        private bool _isDeviceBoot0Mode = false;

        /// <summary>"Logger clock": loggers are set to UTC (default, needed for GNSS) or to this PC's local time.
        /// Saved; the logger keeps the choice with its clock and stamps it into its data.</summary>
        public bool IsClockUTC
        {
            get => _isClockUTC;
            set
            {
                if (_isClockUTC == value) return;
                this.RaiseAndSetIfChanged(ref _isClockUTC, value);
                this.RaisePropertyChanged(nameof(LoggerClockIndex));
                TextDateTimeNow = ClockNowText();

                var cfg = SettingsService.Current;
                if (cfg.Ui.LoggerClockUtc != value)
                {
                    cfg.Ui.LoggerClockUtc = value;
                    SettingsService.Instance.Save();
                }
            }
        }

        private bool _isClockUTC = true;

        /// <summary>The "Logger clock" selector in the header: 0 = UTC, 1 = this PC's local time.</summary>
        public int LoggerClockIndex
        {
            get => IsClockUTC ? 0 : 1;
            set { if (value >= 0) IsClockUTC = value == 0; }
        }

        /// <summary>Local item of the selector, with this PC's current offset ("Local (UTC+02:00)").</summary>
        public string LoggerClockLocalText =>
            "Local (" + DisplayFormat.UtcOffset((int)Math.Round(TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes)) + ")";

        /// <summary>The time Set Time would write now, in the selected logger clock.</summary>
        private string ClockNowText() => DisplayFormat.DateTime(IsClockUTC ? DateTime.UtcNow : DateTime.Now);

        public string TextDateTimeNow
        {
            get => _textDateTimeNow;
            set => this.RaiseAndSetIfChanged(ref _textDateTimeNow, value);
        }

        private string _textDateTimeNow = DisplayFormat.DateTime(DateTime.UtcNow);

        /// <summary>
        /// The device highlighted in the list. Change notification is dispatched to the
        /// UI thread (the periodic pollers assign this from worker threads) and feeds
        /// the KOL mode panel (MainViewViewModel.Kol.cs).
        /// </summary>
        public LoggerDevice? SelectedLoggerDevice
        {
            get => _selectedLoggerDevice;
            set
            {
                if (ReferenceEquals(_selectedLoggerDevice, value)) return;
                _selectedLoggerDevice = value;
                RunOnUi(() =>
                {
                    this.RaisePropertyChanged(nameof(SelectedLoggerDevice));
                    this.RaisePropertyChanged(nameof(IsNanotagSelected));
                    OnSelectedLoggerDeviceChanged();
                });
            }
        }
        private LoggerDevice? _selectedLoggerDevice;

        private System.Timers.Timer? _timer;
        private System.Timers.Timer? _timerClock;
        private bool IsClosing = false;

        private string textMessageBottom;
        public string TextMessageBottom
        {
            get => textMessageBottom;
            set => this.RaiseAndSetIfChanged(ref textMessageBottom, value);
        }

        public SelectionModel<LoggerDevice>? SelectedLoggerDeviceModel { get; }


        public MainViewViewModel()
        {
            _globalDockAdapter = new DockAdapter();
            _globalDockAdapter.ConnectionEvent += _globalDockAdapter_ConnectionEvent;
            _deviceUsbAdapter = new DeviceUsbAdapter();
            _deviceUsbAdapter.ConnectionEvent += _deviceUsbAdapter_ConnectionEvent;

            SelectedLoggerDeviceModel = new SelectionModel<LoggerDevice>();
            SelectedLoggerDeviceModel.SelectionChanged += SelectedLoggerDeviceModel_SelectionChanged;

            _timer = new System.Timers.Timer();
            _timer.Elapsed += _timer_Elapsed;
            _timer.Interval = 2150;

            _timerClock = new System.Timers.Timer();
            _timerClock.Elapsed += _timerClock_Elapsed;
            _timerClock.Interval = 1000;

            Categories = new List<CategoryBase>();

            var recordingsViewModel = new RecordingParsingViewModel(this);
            Categories.Add(new Category { Name = "Recordings", Page = typeof(RecordingsParsing), DataContext = recordingsViewModel, Icon = Symbol.ContactInfo, ToolTip = "Import, Parse and decode recordings" });
            Categories.Add(new Category { Name = "Configuration", Page = typeof(ScheduleEditor), DataContext = new ScheduleEditorViewModel(), Icon = Symbol.TargetEdit, ToolTip = "Edit configuration file" });
            Categories.Add(new Category { Name = "Device Tests", Page = typeof(DeviceTests), DataContext = new DeviceTestsViewModel(this), Icon = Symbol.Repair, ToolTip = "Per-sensor test validations (microphones, GNSS/RF, …)" });
            Categories.Add(new Category { Name = "Live View", Page = typeof(LiveView), DataContext = new LiveViewViewModel(this), Icon = Symbol.Audio, ToolTip = "Real-time microphone view: scope, spectrum, spectrogram, PDM and sound recognition (KOL 4-ch USB stream or PC microphone)" });
            Categories.Add(new Category { Name = "Clock", Page = typeof(ClockCalibration), DataContext = new ClockCalibrationViewModel(this), Icon = Symbol.Clock, ToolTip = "Precise clock sync against NTP and per-unit crystal calibration (KOL, Vesper, Pipistrelle, Nanotag)" });
            var nanotagTools = new NanotagToolsViewModel(this);
            _nanotagConsoleCategory = new Category { Name = "Nanotag Console", Page = typeof(NanotagTools), DataContext = nanotagTools, Icon = Symbol.Tag, ToolTip = "Nanotag disk health, the configuration on the tag, and its serial console (shown while a Nanotag is connected)", IsVisible = false };
            Categories.Add(_nanotagConsoleCategory);
            nanotagTools.WhenAnyValue(t => t.IsConsoleOpen).Subscribe(_ => UpdateNanotagConsoleVisibility());
            Categories.Add(new VesperApp.Models.Separator());
            Categories.Add(new Category { Name = "Software Upgrades", Page = typeof(UpdateChecker), DataContext = new UpdateCheckerViewModel(), Icon = Symbol.New, ToolTip = "Software Upgrades" });
            Categories.Add(new Category { Name = "Firmware Upgrades", Page = typeof(FirmwareUpgrades), DataContext = new FirmwareUpgradesViewModel(this), Icon = Symbol.Upload, ToolTip = "Firmware Upgrades" });
            Categories.Add(new Category { Name = "Plugins", Page = typeof(PluginUpdates), DataContext = new PluginUpdatesViewModel(), Icon = Symbol.Download, ToolTip = "Plugins: GNSS decoder, GNSS RF test" });

            // Open-core seam: entitlement-gated feature modules (cloud sync, GNSS
            // post-processing, remote BLE download) contribute their own nav entries.
            // The open-source build ships no such modules, so this appends nothing
            // today. See docs/ARCHITECTURE.md.
            foreach (var moduleCategory in ASD.Modules.ModuleHost.Instance
                         .GetEntitledNavCategories(ASD.Platform.PlatformServices.Entitlements))
                Categories.Add(moduleCategory);

            Categories.Add(new Category { Name = "Settings", Page = typeof(SettingsView), DataContext = new SettingsViewModel(), Icon = Symbol.Setting, ToolTip = "Application settings" });
            Categories.Add(new Category { Name = "Help", Page = typeof(HelpView), DataContext = new HelpViewModel(), Icon = Symbol.Help, ToolTip = "Help Documentation" });

            ToggleDeviceConsoleCommand = ReactiveCommand.Create(
                () => { IsDeviceConsoleExpanded = !IsDeviceConsoleExpanded; });

            InitKolCommands();

            var settings = SettingsService.Current;
            _isDeviceConsoleExpanded = settings.Ui.DeviceConsoleExpanded;
            _isClockUTC = settings.Ui.LoggerClockUtc;

            // Ensure the working directory exists on every launch (first run creates
            // ~/Documents/MyVesperData) — imports and decodes land there, so it must exist.
            string workingDir = SettingsService.Instance.ResolveWorkingDirectory();

            // The Recordings data browser is the live main view of the working directory:
            // always rooted there at startup, kept current by a FileSystemWatcher.
            recordingsViewModel.LoadDataFolder(workingDir);

            // Honour the configured startup tab (defaults to Recordings).
            Category? startupCategory = Categories.OfType<Category>()
                .FirstOrDefault(c => string.Equals(c.Name, settings.Ui.StartupCategory, StringComparison.OrdinalIgnoreCase));
            SelectedCategory = startupCategory ?? (object)Categories[0];

            #region Dock Commands
            ConnectDisconnectDockCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (_isConnected == true)
                {
                    await _globalDockAdapter.DockDisconnect();

                    //IsConnected = _globalDockAdapter.IsConnected;
                }
                else
                {
                    DockPickWindow dockPickDialog = new DockPickWindow();
                    var dockPickVM = new DockPickWindowViewModel(_globalDockAdapter);
                    dockPickDialog.DataContext = dockPickVM;

                    if (App.MainWindow != null)
                    {
                        _timer.Stop();
                        await Task.Delay(100);
                        var result = await dockPickDialog.ShowDialog<DockDeviceInfo?>(App.MainWindow);

                        if (result != null)
                        {
                            var dockdevice = await _globalDockAdapter.GetDockBySerialNumberAsync(result.Id ?? string.Empty);

                            bool connected = dockdevice != null
                                && await _globalDockAdapter.DockConnect(dockdevice);
                            if (!connected)
                            {
                                string hint = OperatingSystem.IsWindows()
                                    ? "Make sure the dock is plugged in and the FTDI D2XX driver is installed, then try again."
                                    : "Make sure the dock is plugged in and your user is allowed to access it. " +
                                      "On Linux this usually means installing the VesperApp udev rules " +
                                      "(see Help → Linux Setup) and replugging the dock.";
                                string? detail = _globalDockAdapter.LastConnectError;
                                if (!string.IsNullOrEmpty(detail))
                                    hint += $"\n\nDetails: {detail}";
                                await MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
                                {
                                    ButtonDefinitions = ButtonEnum.Ok,
                                    ContentTitle = "Docking station",
                                    ContentHeader = "Could not connect to the docking station.",
                                    ContentMessage = hint,
                                    Icon = MsBox.Avalonia.Enums.Icon.Warning,
                                    WindowIcon = App.MainWindow?.Icon,
                                }).ShowWindowDialogAsync(App.MainWindow);
                            }

                            //IsConnected = _globalDockAdapter.IsConnected;
                        }
                        _timer.Start();
                    }
                }
            });

            EnableDeviceDockCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (await _globalDockAdapter.SetEnableDevice(!this.IsDeviceEnabled) == true)
                {
                    IsDeviceEnabled = !IsDeviceEnabled;
                }
                else
                {
                    await _globalDockAdapter.DockDisconnect();
                }
            });

            Boot0ModeDockCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (await _globalDockAdapter.SetBoot0Mode(!this.IsDeviceBoot0Mode) == true)
                {
                    IsDeviceBoot0Mode = !IsDeviceBoot0Mode;
                }
                else
                {
                    await _globalDockAdapter.DockDisconnect();
                }
            });

            ResetDeviceDockCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (await _globalDockAdapter.ResetDevice() == false)
                {
                    await _globalDockAdapter.DockDisconnect();
                }
            });


            #endregion

            #region Device Commands
            ConnectDisconnectDeviceCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (_isDeviceConnected == true && _deviceUsbAdapter != null && IsClosing == false)
                {
                    try
                    {
                        IsClosing = true;
                        await _deviceUsbAdapter.DeviceDisconnect();
                    }
                    catch (Exception ex)
                    { }
                    finally
                    {
                        IsClosing = false;
                    }
                }
                else if (_deviceUsbAdapter != null && _isDeviceConnected == false && SelectedLoggerDevice != null)
                {
                    if (this.SelectedLoggerDevice.IsComportDevice)
                    {
                        this._timer?.Stop();
                        await Task.Delay(250);
                    }
                    bool connected = false;
                    try
                    {
                        connected = await _deviceUsbAdapter.DeviceConnect(SelectedLoggerDevice);
                    }
                    catch (Exception ex)
                    { }
                    finally
                    {
                        if (this._timer?.Enabled == false)
                            this._timer.Start();
                    }
                    if (connected)
                        await OnDeviceConnectedAsync();   // KOL: read its USB mode on connect

                }
            });

            DownloadDeviceData = ReactiveCommand.CreateFromTask(async () =>
            {
                if (SelectedLoggerDevice != null && !SelectedLoggerDevice.IsNanotagVendor)
                {
                    await DownloadFromUnitDiskAsync(SelectedLoggerDevice);   // Vesper / Pipistrelle / KOL: copy the drive
                    return;
                }
                if (SelectedLoggerDevice != null)
                    await DownloadNanotagAsync(SelectedLoggerDevice);
            });

            BootloaderDeviceCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (SelectedLoggerDevice != null)
                {
                    if (SelectedLoggerDevice.IsKol)
                    {
                        // KOL flashes from the Firmware Upgrades page (commanded DFU entry +
                        // the STM32 DFU flasher, no dock). Sending the bare bootloader
                        // command here would strand the user in DFU with nothing to flash.
                        Category? fw = Categories.OfType<Category>()
                            .FirstOrDefault(c => c.DataContext is FirmwareUpgradesViewModel);
                        if (fw != null)
                        {
                            if (fw.DataContext is FirmwareUpgradesViewModel fwvm)
                                fwvm.SelectedDeviceType = DeviceTypes.Kol;
                            SelectedCategory = fw;
                        }
                        return;
                    }
                    await SelectedLoggerDevice.Bootloader();
                }
            });

            UploadDeviceConfig = ReactiveCommand.CreateFromTask(async () =>
            {
                // if (SelectedLoggerDevice != null)
                // {

                FilePickerOpenOptions foptions = new()
                {
                    Title = "Upload configuration file ...",
                    AllowMultiple = false,

                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new("Configuration JSON file (.json)")
                        {
                            Patterns = new[]{"*.json"},
                            MimeTypes = new[]{"JSON/*"},
                            AppleUniformTypeIdentifiers = new[]{"utf8PlainText"}
                        }
                    },
                };

                Task<IReadOnlyList<IStorageFile>> dialog = RootTopLevel!.StorageProvider!.OpenFilePickerAsync(foptions);

                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {

                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;

                        if (files != null)
                        {
                            var file = files.FirstOrDefault();

                            if (file != null)
                            {
                                string jsonString = string.Empty;
                                try
                                {
                                    string? lp = file.TryGetLocalPath();
                                    if (lp != null)
                                    {
                                        jsonString = File.ReadAllText(lp);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine(ex.Message);
                                    jsonString = string.Empty;
                                }

                                await UploadConfigAsync(SelectedLoggerDevice!, jsonString);
                            }
                        }
                    }
                    catch (Exception ec)
                    {
                        Debug.WriteLine(ec.Message);
                    }

                });
            });

            SetDateTimeDeviceCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                LoggerDevice? dev = SelectedLoggerDevice;
                if (dev == null) return;
                await SetDeviceClockAsync(dev);
                if (dev.IsKol)
                {
                    await RefreshKolUsbModeAsync();      // "clock not set" may be all that kept it from recording
                    KolStatusText = string.Empty;
                }
            });

            SleepDeviceCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (SelectedLoggerDevice != null)
                {
                    await SelectedLoggerDevice.Sleep(false);
                }
            });

            ArmSleepDeviceCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                LoggerDevice? dev = SelectedLoggerDevice;
                if (dev == null) return;
                if (dev.IsNanotagVendor && dev.FirmwareVersion != null && dev.FirmwareVersion >= LoggerDevice.NanotagDiskHealthFirmware)
                {
                    // A deployment starts from a formatted tag: the tag appends and never erases while recording.
                    var health = await dev.GetDiskHealthAsync();
                    if (health != null && health.PagesWritten > 0 &&
                        !await ConfirmAsync("Arm", $"The tag still holds {health.PagesWritten} pages from an earlier deployment.",
                            "A new deployment should start from a formatted tag: Download Data, then Format. " +
                            "Arm anyway? The new data is added after the old."))
                        return;

                    // The clock base travels with the data (UTC / local); an unknown base cannot be decoded to UTC.
                    var clock = await dev.GetClockAsync();
                    if (clock != null && !clock.Value.Base.IsKnown &&
                        await ConfirmAsync("Arm", "The tag's clock was not set by VesperApp (UTC or local is not known).",
                            $"Set the clock now ({(IsClockUTC ? "UTC" : "this PC's local time")}, network time) before arming?"))
                        await SetDeviceClockAsync(dev);
                }
                await dev.Sleep(true);
            });

            FormatDeviceCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                LoggerDevice? dev = SelectedLoggerDevice;
                if (dev == null || dev.IsKol) return;           // KOL: no format in its firmware (button disabled)
                if (dev.IsNanotagVendor)
                {
                    if (!await ConfirmAsync("Format", $"Erase Nanotag {dev.SerialNumber}?",
                            "All recorded data on the tag is erased. Download Data first if you still need it."))
                        return;
                    if (dev.FirmwareVersion != null && dev.FirmwareVersion >= LoggerDevice.NanotagDiskHealthFirmware)
                        await FormatNanotagAsync(dev);
                    else
                        TextMessageBottom = await dev.FormatDisk()
                            ? "Format sent (Nanotag firmware before 1.39 reports no progress: wait about 15 s before the next action)."
                            : "The Nanotag did not answer the format command in time; it may still be erasing. Wait 15 s, then check it on the Nanotag page.";
                    return;
                }
                if (dev.IsComportDevice)
                    await FormatUnitDiskAsync(dev);
            });
            #endregion

            LoggerDevices = new ObservableCollection<LoggerDevice>();
            LoggerDevices.CollectionChanged += (_, _) => RunOnUi(UpdateNanotagConsoleVisibility);
            SelectedLoggerDevice = null;
            OpenNanotagConsoleCommand = ReactiveCommand.Create(() =>
            {
                _nanotagConsoleCategory.IsVisible = true;
                SelectedCategory = _nanotagConsoleCategory;
            });
            
            _timer.Start();
            _timerClock.Start();

            TextMessageBottom = string.Empty;
		}


        /// <summary>Format a Vesper / Pipistrelle drive: pick the file system (exFAT only on 32 / 64 GB units with
        /// firmware 1.22+), confirm, then wait for the unit's result (it takes the drive away from the PC while it
        /// formats and restarts afterwards).</summary>
        private async Task FormatUnitDiskAsync(LoggerDevice dev)
        {
            ulong? bytes = await dev.GetDiskBytesAsync();
            IReadOnlyList<DiskFileSystem> choices = DiskFormat.Choices(dev.FirmwareVersion, bytes);
            string size = bytes is ulong b ? $"{b / 1e9:0.#} GB storage" : "storage of unknown size";
            string fw = dev.FirmwareVersion is Version v ? $"firmware {v.Major}.{v.Minor}" : "unknown firmware";

            string note = choices.Count > 1
                ? "FAT32: what the unit has always used. exFAT: no FSInfo free-space hint to go stale, a faster format; " +
                  "Windows 10+, macOS and Linux read it. Both use 32 KB clusters (one recorder write per cluster)."
                : (bytes is ulong b2 && b2 < DiskFormat.ExFatMinBytes)
                    ? "FAT32 with 32 KB clusters (exFAT is offered on 32 / 64 GB units only)."
                    : "FAT32 with 32 KB clusters (exFAT needs firmware 1.22 or later and a 32 / 64 GB unit).";

            var buttons = new List<ButtonDefinition>();
            foreach (DiskFileSystem fs in choices)
                buttons.Add(new ButtonDefinition { Name = "Format " + DiskFormat.Name(fs), IsDefault = fs == choices[0] });
            buttons.Add(new ButtonDefinition { Name = "Cancel", IsCancel = true });

            var box = MessageBoxManager.GetMessageBoxCustom(new MessageBoxCustomParams
            {
                ButtonDefinitions = buttons,
                ContentTitle = "Format",
                ContentHeader = $"Erase {dev.DeviceType} {dev.SerialNumber}? ({size}, {fw})",
                ContentMessage = "Every file on the unit's drive is erased: recordings, config.json, the logs and RTCCAL.TXT " +
                                 "(the clock calibration stays in the unit). Download Data first if you still need them." +
                                 Environment.NewLine + Environment.NewLine + note + Environment.NewLine + Environment.NewLine +
                                 "Close any file or window open on the drive. The drive disappears while the unit formats " +
                                 "(up to a minute on 64 GB FAT32), then the unit restarts.",
                Icon = Icon.Warning,
                WindowIcon = App.MainWindow?.Icon,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                CanResize = false,
            });
            string answer = App.MainWindow != null ? await box.ShowWindowDialogAsync(App.MainWindow) : await box.ShowAsync();
            DiskFileSystem? chosen = null;
            foreach (DiskFileSystem fs in choices)
                if (answer == "Format " + DiskFormat.Name(fs)) chosen = fs;
            if (chosen is not DiskFileSystem pick) return;

            IsFormatting = true;
            try
            {
                RunOnUi(() => TextMessageBottom = $"Formatting {dev.SerialNumber} as {DiskFormat.Name(pick)}… keep it docked.");
                DiskFormatResult r = await dev.FormatDiskAsync(pick, DiskFormat.DefaultClusterKb);
                string msg = DiskFormat.Message(r, pick);
                RunOnUi(() => TextMessageBottom = msg);
            }
            finally
            {
                IsFormatting = false;
            }
        }

        /// <summary>Set the device clock (Set Time, and before a KOL starts recording).</summary>
        /// <summary>Format a Nanotag (firmware 1.39+): it answers at once and erases in steps, so follow the progress
        /// (GET_DISKHEALTH), then report the bad blocks against the count before the format - a format must not
        /// change it unless a block failed - and show the block map on the Nanotag page.</summary>
        private async Task FormatNanotagAsync(LoggerDevice dev)
        {
            NanotagDiskHealth? before = await dev.GetDiskHealthAsync();
            if (!await dev.FormatDisk())
            {
                RunOnUi(() => TextMessageBottom = "The Nanotag did not accept the format command.");
                return;
            }

            NanotagDiskHealth? h = null;
            bool seenRunning = false;
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < TimeSpan.FromSeconds(120))
            {
                await Task.Delay(500);
                h = await dev.GetDiskHealthAsync();
                if (h == null) continue;
                if (h.Formatting)
                {
                    seenRunning = true;
                    int pct = h.FormatPercent;
                    RunOnUi(() => TextMessageBottom = $"Formatting the Nanotag… {pct} %");
                    continue;
                }
                if (seenRunning || sw.Elapsed > TimeSpan.FromSeconds(3)) break;   // done (a short format may finish between polls)
            }

            string message;
            if (h == null || h.Formatting)
                message = "The format did not report completion; check the Nanotag Console.";
            else
            {
                long added = before != null ? (long)h.BadBlocks - before.BadBlocks : 0;
                message = $"Format done: {h.BadBlocks} bad blocks" +
                          (before == null ? "." : added == 0 ? " (unchanged)." : $" ({added:+#;-#} since before the format).");
            }
            RunOnUi(() => TextMessageBottom = message);

            Category? page = Categories.OfType<Category>().FirstOrDefault(c => c.DataContext is NanotagToolsViewModel);
            if (page?.DataContext is NanotagToolsViewModel nvm)
                await Dispatcher.UIThread.InvokeAsync(nvm.RefreshAsync);
        }

        /// <summary>Upload Config: a Nanotag gets the config over USB (binary rows); a Vesper / Pipistrelle /
        /// KOL gets it as config.json on its drive (DeviceDiskTransfer), and a KOL with the operating states
        /// is told to re-read it.</summary>
        private async Task UploadConfigAsync(LoggerDevice dev, string json)
        {
            string message;

            if (dev.IsNanotagVendor)
            {
                bool ok = await dev.UploadConfigFile(json);
                message = ok ? "Configuration uploaded to the Nanotag." : "Configuration upload to the Nanotag failed.";
            }
            else
            {
                string uid = dev.SerialNumber ?? string.Empty;
                DeviceDiskTransfer.Result r = await Task.Run(() =>
                    DeviceDiskTransfer.WriteConfig(UnitDisk.FindRoot(uid), json));
                message = r.Message;
                if (r.Ok && dev.IsKol)
                {
                    await dev.GetUsbModeAsync(rereadConfig: true);
                    message += " (KOL re-read it)";
                }
            }
            RunOnUi(() => TextMessageBottom = message);
        }

        /// <summary>Download Data on a Nanotag, finishing like a Vesper import: target suggested as
        /// working folder / tag ID / first record date, the device actions locked while the tag is read, an
        /// incomplete read reported as such, then the session opened on the Recordings page and decoded
        /// (Settings → Auto Decode).</summary>
        private async Task DownloadNanotagAsync(LoggerDevice dev)
        {
            IsNanotagDownloading = true;
            bool unlocked = false;
            try
            {
                long pages = 0;
                DateTime? firstRecord = await Task.Run(() => dev.ReadFirstRecordTime(out pages));
                string id = string.IsNullOrWhiteSpace(dev.SerialNumber) ? "Nanotag" : dev.SerialNumber!;
                string suggested = Path.Combine(SettingsService.Instance.ResolveWorkingDirectory(), id,
                    (firstRecord ?? DateTime.Now).ToString("yyyy-MM-dd_HH-mm-ss"));

                string? path = await ChooseNanotagTargetAsync(id, pages, firstRecord, suggested);
                if (path == null) return;
                Directory.CreateDirectory(path);

                TextMessageBottom = $"Downloading Nanotag {id} to {path}. Keep the tag in the dock until it has finished.";
                // The whole download off the UI thread (its first USB transfer ran on the caller's thread).
                bool ok = await Task.Run(() => dev.DownloadPages(path));

                string attention = File.Exists(Path.Combine(path, "download-read-status.txt"))
                    ? " Some pages need attention: see download-read-status.txt." : string.Empty;
                if (ok)
                    TextMessageBottom = $"Downloaded {dev.DownloadPagesDone} pages to {path}.{attention}";
                else if (dev.DownloadStopReason != null)
                    TextMessageBottom = $"Download incomplete: {dev.DownloadStopReason}. What was read is in {path}; dock the tag and download again.";
                else
                    TextMessageBottom = "Download failed: reconnect the tag and try again.";

                IsNanotagDownloading = false;
                unlocked = true;

                if (dev.DownloadPagesDone > 0)
                {
                    Category? rec = Categories.OfType<Category>().FirstOrDefault(c => c.DataContext is RecordingParsingViewModel);
                    if (rec?.DataContext is RecordingParsingViewModel rvm)
                    {
                        SelectedCategory = rec;
                        if (ok) _ = rvm.FinishNanotagDownloadAsync(path);    // decode runs as a job; the toolbar is free
                        else rvm.LoadDataFolder(path);
                    }
                }
            }
            catch (Exception ex)
            {
                TextMessageBottom = "Download failed: " + ex.Message;
            }
            finally
            {
                if (!unlocked) IsNanotagDownloading = false;
            }
        }

        /// <summary>Where a Nanotag download goes: the suggested session folder, or one the user picks.
        /// Null when cancelled.</summary>
        private async Task<string?> ChooseNanotagTargetAsync(string id, long pages, DateTime? firstRecord, string suggested)
        {
            string what = pages > 0
                ? $"Nanotag {id} · {pages:N0} pages ({pages * 4096 / 1_000_000.0:N1} MB)" +
                  (firstRecord is DateTime f ? $" · first record {f:yyyy-MM-dd HH:mm}" : string.Empty)
                : $"Nanotag {id} · no recorded data found";

            var box = MessageBoxManager.GetMessageBoxCustom(new MessageBoxCustomParams
            {
                ButtonDefinitions = new List<ButtonDefinition>
                {
                    new() { Name = "Download", IsDefault = true },
                    new() { Name = "Choose folder…" },
                    new() { Name = "Cancel", IsCancel = true },
                },
                ContentTitle = "Download Nanotag data",
                ContentHeader = what,
                ContentMessage = "Download to:" + Environment.NewLine + suggested + Environment.NewLine + Environment.NewLine +
                                 "Keep the tag in the dock until the download has finished. The tag is not erased.",
                Icon = Icon.Question,
                WindowIcon = App.MainWindow?.Icon,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                CanResize = false,
            });
            string answer = App.MainWindow != null ? await box.ShowWindowDialogAsync(App.MainWindow) : await box.ShowAsync();

            if (answer == "Download") return suggested;
            if (answer != "Choose folder…") return null;

            IReadOnlyList<IStorageFolder> folders = await RootTopLevel!.StorageProvider!.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Download Nanotag data to…",
                AllowMultiple = false,
            });
            return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        }

        /// <summary>Download Data on a Vesper / Pipistrelle / KOL: the guided import (Recordings page) with the
        /// unit's drive pre-selected, which copies the recorded files and auto-decodes them per Settings.</summary>
        private async Task DownloadFromUnitDiskAsync(LoggerDevice dev)
        {
            string uid = dev.SerialNumber ?? string.Empty;
            string? root = await Task.Run(() => UnitDisk.FindRoot(uid));
            if (root == null)
            {
                RunOnUi(() => TextMessageBottom = DeviceDiskTransfer.DriveNotFound);
                return;
            }
            Category? rec = Categories.OfType<Category>().FirstOrDefault(c => c.DataContext is RecordingParsingViewModel);
            if (rec?.DataContext is not RecordingParsingViewModel rvm) return;
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                SelectedCategory = rec;
                await rvm.ImportFromDeviceAsync(root);
            });
        }

        private async Task SetDeviceClockAsync(LoggerDevice dev)
        {
            /* True time from NTP: the PC clock alone can be seconds off (bench 2026-10-21:
               2.5 s fast). Loggers with the time-sync firmware (KOL / VesperU5, Nanotag 1.37+)
               get the precise path (as the Clock page: recorded for the per-unit calibration);
               the rest a plain set with NTP-corrected time. Either way the clock base (UTC /
               local, from the "Logger clock" setting) goes with it: the logger keeps it. */
            TextMessageBottom = "Setting the device clock…";
            var rc = await VesperApp.Services.Time.ReferenceClock.CreateAsync();
            var clockBase = VesperApp.Services.Time.DeviceClockBase.For(IsClockUTC, rc.TrueUtcNow());
            VesperApp.Services.Time.IRtcDevice? precise =
                dev.IsConsoleLogger ? new VesperApp.Services.Time.LoggerRtcDevice(dev)
                : dev.DeviceType == DeviceTypes.Nanotag ? new VesperApp.Services.Time.NanotagRtcDevice(dev) : null;
            if (precise != null)
            {
                var r = await VesperApp.Services.Time.RtcSyncService.SyncAsync(precise, rc, IsClockUTC,
                    VesperApp.Services.Time.RtcCalibrationStore.Default);
                if (r.Before != null)                               // the unit time-syncs (KOL / VesperU5, Nanotag 1.37+)
                {
                    TextMessageBottom = $"Device clock in {clockBase.Describe()}. {r.Message}";
                    return;
                }
            }
            DateTime trueUtc = rc.TrueUtcNow();
            DateTime sdt = IsClockUTC ? trueUtc : trueUtc.ToLocalTime();
            bool ok = await dev.SetDateTime(sdt, clockBase);
            TextMessageBottom = ok
                ? $"Device clock set to {sdt:yyyy-MM-dd HH:mm:ss} {clockBase.Describe()} ({(rc.IsNtp ? "NTP" : "PC clock, NTP not reachable")}; whole seconds)."
                : "Setting the device clock failed: reconnect the logger and try again.";
        }

        private void _globalDockAdapter_ConnectionEvent(object? sender, DockConnectionEventArgs e)
        {
            if (e.IsConnected != IsConnected)
            {
                IsConnected = e.IsConnected;

                // The console is where the freshly connected dock's devices appear — surface it
                // even if the user had collapsed it for offline work.
                if (e.IsConnected)
                    IsDeviceConsoleExpanded = true;
            }
        }

        private void _deviceUsbAdapter_ConnectionEvent(object? sender, DeviceConnectionEventArgs e)
        {
            IsDeviceConnected = e.IsConnected;
            RunOnUi(RaiseKolProperties);   // CanUseDeviceDisk follows the connection state
        }



        private async Task ScanFor(int vid, int pid)
        {
            if (_deviceUsbAdapter != null)
            {
                var devices = await _deviceUsbAdapter.ScanDevicesAsync((uint)vid,(uint)pid, true);
                foreach (LoggerDevice logDevice in devices)
                {
                    if (LoggerDevices.Contains(logDevice) == false)
                        LoggerDevices.Add(logDevice);
                }

                LoggerDevice[] list = new LoggerDevice[LoggerDevices.Count];
                LoggerDevices.CopyTo(list, 0);

                foreach (LoggerDevice dev in list)
                {
                    if (dev.IsComportDevice == false)
                    {
                        bool f = false;
                        foreach (LoggerDevice lDevice in devices)
                        {
                            if (lDevice.Equals(dev))
                            {
                                f = true;
                                break;
                            }
                        }

                        if (f == false && dev.IsConnected == false) LoggerDevices.Remove(dev);
                    }
                }
            }
        }
        private async Task ScanForComport()
        {
            if (_deviceUsbAdapter != null)
            {
                var devices = await _deviceUsbAdapter.ScanComPortsAsync(false);
                foreach (LoggerDevice logDevice in devices)
                {
                    if (LoggerDevices.Contains(logDevice) == false)
                    {
                        //Debug.WriteLine("Added: " + logDevice.SerialNumber);
                        LoggerDevices.Add(logDevice);
                    }
                }

                LoggerDevice[] list = new LoggerDevice[LoggerDevices.Count];
                LoggerDevices.CopyTo(list, 0);

                foreach (LoggerDevice dev in list)
                {
                    if (dev.IsComportDevice)
                    {
                        //Debug.WriteLine("Probing: " + dev.SerialNumber);
                        bool f = false;
                        foreach (LoggerDevice lDevice in devices)
                        {
                            //Debug.WriteLine(" - : " + lDevice.SerialNumber);
                            if (lDevice == dev)
                            {
                                //Debug.WriteLine("Match");
                                f = true;
                                break;
                            }
                        }

                        if (f == false && dev.IsConnected == false)
                        {
                            LoggerDevices.Remove(dev);
                            //Debug.WriteLine("No Match - Remove: " + dev.SerialNumber);
                        }
                    }
                }
            }
        }


        private void _timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (IsDeviceConnected == false && _deviceUsbAdapter != null && IsClosing == false && !IsNanotagDownloading)
            {
                    var scan_nano = Task.Run(async () => await ScanFor(Nanotag.VendorId, Nanotag.ProductId));
                    var scan_comport = Task.Run(async () => await ScanForComport());
            }
        }

        private void _timerClock_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            TextDateTimeNow = ClockNowText();

            if (IsDeviceConnected == true && SelectedLoggerDevice != null && !IsNanotagDownloading)
            {
                var ping_device = Task.Run(async () =>
                {
                    if (SelectedLoggerDevice != null)
                    {
                        if(await SelectedLoggerDevice.GetInfo() == false)
                        {
                            await SelectedLoggerDevice.Disconnect();
                            SelectedLoggerDevice = null;
                        }
                    }
                });
            }
        }

        private void SelectedLoggerDeviceModel_SelectionChanged(object? sender, SelectionModelSelectionChangedEventArgs<LoggerDevice> e)
        {
            if (e.SelectedItems != null && e.SelectedItems.Count > 0)
                SelectedLoggerDevice = e.SelectedItems[0];
        }


        public Interaction<DockPickWindowViewModel, DockDeviceInfo?> ShowDockPickDialog { get; }
        public ICommand? ConnectDisconnectDockCommand { get; }
        public ICommand? ResetDeviceDockCommand { get; }
        public ICommand? Boot0ModeDockCommand { get; }
        public ICommand? EnableDeviceDockCommand { get; }
        public ICommand? ConnectDisconnectDeviceCommand { get; }
        public ICommand? SleepDeviceCommand { get; }
        public ICommand? ArmSleepDeviceCommand { get; }
        public ICommand? FormatDeviceCommand { get; }
        public ICommand? BootloaderDeviceCommand { get; }
        public ICommand? SetDateTimeDeviceCommand { get; }
        public ICommand? UploadDeviceConfig { get; }
        public ICommand? DownloadDeviceData { get; }

        /*
         * Logger Devices Section
        * */
        public ObservableCollection<LoggerDevice> LoggerDevices { get; }


        public List<CategoryBase> Categories { get; }

        private readonly Category _nanotagConsoleCategory;

        public ICommand OpenNanotagConsoleCommand { get; }

        /// <summary>The device list's selection is a Nanotag: the device actions offer the Nanotag Console.</summary>
        public bool IsNanotagSelected => SelectedLoggerDevice?.DeviceType == DeviceTypes.Nanotag;

        /// <summary>
        /// The Nanotag Console is in the menu while a Nanotag is connected. It also stays while it is the open page
        /// or its serial console is open, so a tag that reboots or drops off USB does not take the page away.
        /// </summary>
        private void UpdateNanotagConsoleVisibility()
        {
            if (_nanotagConsoleCategory == null) return;     // during construction
            _nanotagConsoleCategory.IsVisible =
                LoggerDevices?.Any(d => d.DeviceType == DeviceTypes.Nanotag) == true
                || ReferenceEquals(SelectedCategory, _nanotagConsoleCategory)
                || (_nanotagConsoleCategory.DataContext as NanotagToolsViewModel)?.IsConsoleOpen == true;
        }

        public object SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedCategory, value);
                SetCurrentPage();
                UpdateNanotagConsoleVisibility();
            }
        }
        public Control CurrentPage
        {
            get => _currentPage;
            set => this.RaiseAndSetIfChanged(ref _currentPage, value);
        }

        /// <summary>Load an already-validated configuration JSON into the Configuration
        /// editor and navigate to that tab. Used by the Recordings browser when the user
        /// double-clicks a recognised config file.</summary>
        public async Task OpenConfigurationInEditor(string json)
        {
            Category? cat = Categories.OfType<Category>()
                .FirstOrDefault(c => string.Equals(c.Name, "Configuration", StringComparison.OrdinalIgnoreCase));
            if (cat?.DataContext is not ScheduleEditorViewModel editor) return;

            if (await editor.LoadConfigurationFromJsonAsync(json))
                SelectedCategory = cat;
        }

        private void SetCurrentPage()
        {
            if (SelectedCategory is Category cat)
            {
                if (cat.Page != null)
                {
                    var pg = Activator.CreateInstance(cat.Page);
                    CurrentPage = (Control)pg;
                    CurrentPage.DataContext = cat.DataContext;
                }
            }
            else if (SelectedCategory is NavigationViewItem nvi)
            {
                //var smpPage = $"FAControlsGallery.Pages.NVSamplePages.NVSamplePageSettings";
                //var pg = Activator.CreateInstance(Type.GetType(smpPage));
                //CurrentPage = (Control)pg;
            }
        }

        private object _selectedCategory;
        private Control _currentPage = new RecordingsParsing();


        public static TopLevel? RootTopLevel { get; set; }
        private static IStorageProvider? _storageProvider;
        public static IStorageProvider? StorageProvider
        {
            get
            {
                if (_storageProvider != null)
                    return _storageProvider;

                IStorageProvider? rootTopLevelStorageProvider = RootTopLevel?.StorageProvider;
                if (rootTopLevelStorageProvider != null)
                {
                    _storageProvider = rootTopLevelStorageProvider;
                    return _storageProvider;
                }

                //If mainWindow is available (for example for the Desktop variant), we use it to get a storage provider.
                // If not, then we try getting the provider from the root TopLevel instance. (Web, the designer preview,...)
                //TODO doesn't work. I have ho idea how to get a TopLevel instance in a Web, preview or Android/iOS environment.
                MainWindow? mainWindow = (MainWindow?)App.MainWindow;
                _storageProvider = mainWindow != null ? mainWindow.StorageProvider : null;

                if (_storageProvider == null)
                    throw new InvalidOperationException("StorageProvider platform implementation is not available.");

                return _storageProvider;
            }
            set => _storageProvider = value;
        }

    }
}
