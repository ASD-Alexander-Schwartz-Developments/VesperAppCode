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
        // ReleaseFeedService. See docs/ci-templates/firmware-release.yml / docs/ARCHITECTURE.md.
        private const string DefaultFirmwareFeed =
            "https://d11eqmwet07q29.cloudfront.net/firmware/index.json";

        /// <summary>The firmware feed URL — overridable via <c>VESPERAPP_FIRMWARE_FEED</c> so the
        /// CDN prefix can change without a rebuild.</summary>
        private static Uri FirmwareFeedUrl()
        {
            string? overrideUrl = Environment.GetEnvironmentVariable("VESPERAPP_FIRMWARE_FEED");
            return new Uri(string.IsNullOrWhiteSpace(overrideUrl) ? DefaultFirmwareFeed : overrideUrl);
        }

        private readonly ReleaseFeedService feedService;

        // Every entry from the last feed read; Releases is this list filtered to SelectedDeviceType.
        private readonly List<ReleaseEntry> allReleases = new();

        public ICommand? RefreshReleasesCommand { get; }
        public ICommand? DownloadReleaseCommand { get; }

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

        public FirmwareUpgradesViewModel()
        {
            RefreshReleasesCommand = ReactiveCommand.CreateFromTask(RunReleasesRefresh);
            DownloadReleaseCommand = ReactiveCommand.CreateFromTask(RunReleaseDownload);

            feedService = new ReleaseFeedService(FirmwareFeedUrl());

            // Load the releases on initialization.
            _ = RunReleasesRefresh();
        }

        private async Task<bool> RunReleasesRefresh()
        {
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
            if (selected is null || string.IsNullOrWhiteSpace(selected.Asset))
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
