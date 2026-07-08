using ASDWaveLib;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Models;
using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using VesperApp.Controls;
using VesperApp.Models;
using VesperApp.Services;
using VesperApp.Views;

namespace VesperApp.ViewModels
{
    public class FirmwareUpgradesViewModel : ViewModelBase
    {
        // Firmware updates come from a CDN release feed (index.json) published to S3/CloudFront by CI
        // in the private firmware repo — NOT the GitHub API, so there is no token in the client at all.
        // The client only reads the feed and downloads assets over plain HTTPS, SHA-256-verified by
        // ReleaseFeedService. The CDN origin is injected at build time (CdnConfig), not a literal here.
        // See docs/CDN-HARDENING.md / docs/ci-templates/firmware-release.yml / docs/ARCHITECTURE.md.
        private const string FirmwareFeedPath = "firmware/index.json";

        /// <summary>The firmware feed URL, or <c>null</c> when no CDN origin is configured in this build.
        /// Overridable via <c>VESPERAPP_FIRMWARE_FEED</c> (full URL) or <c>VESPERAPP_CDN_BASE</c> (origin)
        /// so the CDN prefix can change without a rebuild.</summary>
        private static Uri? FirmwareFeedUrl()
        {
            string? overrideUrl = Environment.GetEnvironmentVariable("VESPERAPP_FIRMWARE_FEED");
            return !string.IsNullOrWhiteSpace(overrideUrl)
                ? new Uri(overrideUrl)
                : CdnConfig.FeedUri(FirmwareFeedPath);
        }

        private readonly ReleaseFeedService? feedService;

        // Every entry from the last feed read; Releases is this list filtered to SelectedDeviceType.
        private readonly List<ReleaseEntry> allReleases = new();

        public ICommand? RefreshReleasesCommand { get; }
        public ICommand? DownloadReleaseCommand { get; }
        public ICommand? FlashCommand { get; }

        private readonly MainViewViewModel? _main;

        private bool isFlashing;
        public bool IsFlashing { get => isFlashing; set => this.RaiseAndSetIfChanged(ref isFlashing, value); }

        private int flashPercent;
        public int FlashPercent { get => flashPercent; set => this.RaiseAndSetIfChanged(ref flashPercent, value); }

        private string flashStatus = string.Empty;
        public string FlashStatus { get => flashStatus; set => this.RaiseAndSetIfChanged(ref flashStatus, value); }

        /// <summary>Releases shown in the grid: the feed, filtered by <see cref="SelectedDeviceType"/>.</summary>
        public ObservableCollection<ReleaseEntry> Releases { get; } = new();

        // Bound TwoWay to the DataGrid's SelectedItem. A plain property: the previous SelectionModel
        // binding never set SelectedItem, so Download had nothing to act on.
        private ReleaseEntry? selectedFirmwareRelease;
        public ReleaseEntry? SelectedFirmwareRelease
        {
            get => selectedFirmwareRelease;
            set => this.RaiseAndSetIfChanged(ref selectedFirmwareRelease, value);
        }

        // Bound to the device-type ComboBox. The firmware feed tags each entry with a Target device
        // key (e.g. "vesper"); changing this re-filters the list. Matched to ReleaseEntry.Target by
        // the enum name, case-insensitive — keep the CI Target values in sync with DeviceTypes.
        private DeviceTypes selectedDeviceType = DeviceTypes.Vesper;
        public DeviceTypes SelectedDeviceType
        {
            get => selectedDeviceType;
            set
            {
                this.RaiseAndSetIfChanged(ref selectedDeviceType, value);
                ApplyDeviceFilter();
            }
        }

        private bool isLoadingReleases = false;
        public bool IsLoadingReleases
        {
            get => isLoadingReleases;
            set => this.RaiseAndSetIfChanged(ref isLoadingReleases, value);
        }

        public FirmwareUpgradesViewModel(MainViewViewModel? main)
        {
            _main = main;

            RefreshReleasesCommand = ReactiveCommand.CreateFromTask(RunReleasesRefresh);
            DownloadReleaseCommand = ReactiveCommand.CreateFromTask(RunReleaseDownload);
            FlashCommand = ReactiveCommand.CreateFromTask(RunFlash);

            Uri? feedUrl = FirmwareFeedUrl();
            feedService = feedUrl is null ? null : new ReleaseFeedService(feedUrl);

            // Surface the missing-feed condition on the page instead of a silent empty
            // list (typical for local dev builds, where CI hasn't baked the CDN origin).
            if (feedService is null)
                FlashStatus = "No update source is configured in this build — the release list is unavailable. "
                            + "Use a released build, or set VESPERAPP_CDN_BASE for a local build.";

            // Load the releases on initialization (no-op when no feed is configured).
            _ = RunReleasesRefresh();
        }

        // Parameterless ctor for the XAML designer / ViewLocator fallback.
        public FirmwareUpgradesViewModel() : this(null) { }

        private async Task<bool> RunReleasesRefresh()
        {
            if (feedService is null)
            {
                Debug.WriteLine("Firmware feed unavailable: no CDN origin configured in this build.");
                return false;
            }
            try
            {
                IsLoadingReleases = true;
                var releases = await feedService.GetReleasesAsync();
                allReleases.Clear();
                allReleases.AddRange(releases);
                ApplyDeviceFilter();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to load firmware feed: " + ex);
                return false;
            }
            finally
            {
                IsLoadingReleases = false;
            }
        }

        // Show only entries whose feed Target matches the selected device (case-insensitive), plus
        // any entry with no Target (treated as applying to every device).
        private void ApplyDeviceFilter()
        {
            string device = SelectedDeviceType.ToString();
            Releases.Clear();
            foreach (var r in allReleases)
            {
                if (string.IsNullOrWhiteSpace(r.Target) ||
                    string.Equals(r.Target, device, StringComparison.OrdinalIgnoreCase))
                {
                    Releases.Add(r);
                }
            }

            // Drop a selection the new filter hides.
            if (SelectedFirmwareRelease is not null && !Releases.Contains(SelectedFirmwareRelease))
                SelectedFirmwareRelease = null;
        }

        private async Task<bool> RunReleaseDownload()
        {
            var selected = SelectedFirmwareRelease;
            if (selected is null || string.IsNullOrWhiteSpace(selected.Asset) || feedService is null)
                return false;

            var filePicker = StorageProvider;
            if (filePicker is null)
                return false;

            // The feed entry's asset path carries the file name and extension (.hex / .bin / .zip).
            string fileName = Path.GetFileName(selected.Asset!);
            string ext = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) ext = "*";

            var fileSaveResult = await filePicker.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Firmware File",
                SuggestedFileName = fileName,
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new FilePickerFileType($"Firmware ({ext})") { Patterns = new[] { $"*.{ext}" } }
                }
            });

            if (fileSaveResult is null)
                return false;

            try
            {
                string filePath = fileSaveResult.Path.LocalPath;
                await feedService.DownloadAssetAsync(selected, filePath);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Firmware download failed: " + ex);
                return false;
            }
        }

        // Download the selected release and flash it to the docked device. STM32 products
        // (Vesper/Kol/Pipistrelle) go through the dock-GPIO USB-DFU path; Nanotag (Microchip
        // bootloader) is not implemented yet.
        private async Task<bool> RunFlash()
        {
            // Order matters for a truthful message: an unconfigured feed or an empty
            // list are the actual problem far more often than a missing selection.
            if (feedService is null)
            {
                FlashStatus = "No update source is configured in this build — releases cannot be listed or flashed. "
                            + "Use a released build, or set VESPERAPP_CDN_BASE for a local build.";
                return false;
            }
            if (Releases.Count == 0)
            {
                FlashStatus = $"No firmware releases loaded for {SelectedDeviceType}. Press Refresh, or check the update source.";
                return false;
            }

            var selected = SelectedFirmwareRelease;
            if (selected is null || string.IsNullOrWhiteSpace(selected.Asset))
            {
                FlashStatus = "Select a firmware release in the list first.";
                return false;
            }

            DeviceTypes target = SelectedDeviceType;

            if (target == DeviceTypes.Nanotag)
            {
                LoggerDevice? ntag = _main?.SelectedLoggerDevice;
                if (ntag is null || ntag.DeviceType != DeviceTypes.Nanotag || !ntag.IsConnected)
                {
                    FlashStatus = "Connect a Nanotag over USB and select it in the device list first.";
                    await ShowInfo("Nanotag firmware update", FlashStatus);
                    return false;
                }
                if (!Path.GetFileName(selected.Asset!).EndsWith(".hex", StringComparison.OrdinalIgnoreCase))
                {
                    FlashStatus = "Nanotag firmware must be an Intel HEX (.hex) file.";
                    await ShowInfo("Nanotag firmware update", FlashStatus);
                    return false;
                }
                if (!await ConfirmFlash(target, Path.GetFileName(selected.Asset!)))
                    return false;

                string ntmp = Path.Combine(Path.GetTempPath(), Path.GetFileName(selected.Asset!));
                IsFlashing = true;
                FlashPercent = 0;
                try
                {
                    FlashStatus = "Downloading firmware…";
                    await feedService.DownloadAssetAsync(selected, ntmp);

                    var prog = new Progress<FlashProgress>(p => { FlashPercent = p.Percent; FlashStatus = p.Status; });
                    await NanotagFlasher.FlashAsync(ntag, ntmp, prog);
                    FlashStatus = "Nanotag firmware updated.";
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Nanotag flash failed: " + ex);
                    FlashStatus = "Flash failed: " + ex.Message;
                    await ShowInfo("Nanotag firmware update failed", FlashStatus);
                    return false;
                }
                finally
                {
                    IsFlashing = false;
                    try { if (File.Exists(ntmp)) File.Delete(ntmp); } catch { }
                }
            }

            DockAdapter? dock = _main?.GlobalDock;
            if (dock is null || !dock.IsConnected)
            {
                FlashStatus = "Connect the docking station first — STM32 flashing drives BOOT0/reset through the dock.";
                await ShowInfo("Firmware update", FlashStatus);
                return false;
            }

            if (!await ConfirmFlash(target, Path.GetFileName(selected.Asset!)))
                return false;

            string tmp = Path.Combine(Path.GetTempPath(), Path.GetFileName(selected.Asset!));

            IsFlashing = true;
            FlashPercent = 0;
            try
            {
                FlashStatus = "Downloading firmware…";
                await feedService.DownloadAssetAsync(selected, tmp);

                var progress = new Progress<FlashProgress>(p =>
                {
                    FlashPercent = p.Percent;
                    FlashStatus = p.Status;
                });

                await Stm32DfuFlasher.FlashAsync(dock, tmp, progress);
                FlashStatus = "Firmware updated — device reset into the new application.";
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Flash failed: " + ex);
                FlashStatus = "Flash failed: " + ex.Message;
                await ShowInfo("Firmware update failed", FlashStatus);
                return false;
            }
            finally
            {
                IsFlashing = false;
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }

        private static async Task<bool> ConfirmFlash(DeviceTypes target, string fileName)
        {
            var box = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
            {
                ButtonDefinitions = ButtonEnum.YesNo,
                ContentTitle = "Flash firmware",
                ContentHeader = $"Flash {target} firmware?",
                ContentMessage = $"This writes '{fileName}' to the docked device and reboots it. Make sure the correct device is docked.\n\nContinue?",
                Icon = MsBox.Avalonia.Enums.Icon.Warning,
                WindowIcon = App.MainWindow?.Icon,
            });
            return await box.ShowWindowDialogAsync(App.MainWindow) == ButtonResult.Yes;
        }

        private static async Task ShowInfo(string title, string message)
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

        private static IStorageProvider? _storageProvider;
        public static IStorageProvider? StorageProvider
        {
            get
            {
                if (_storageProvider != null)
                    return _storageProvider;

                IStorageProvider? rootTopLevelStorageProvider = App.AppTopLevel?.StorageProvider;
                if (rootTopLevelStorageProvider != null)
                {
                    _storageProvider = rootTopLevelStorageProvider;
                    return _storageProvider;
                }

                //If mainWindow is available (for example for the Desktop variant), we use it to get a storage provider.
                // If not, then we try getting the provider from the root TopLevel instance. (Web, the designer preview,...)
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
