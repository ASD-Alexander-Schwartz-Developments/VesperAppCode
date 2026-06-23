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
        // Firmware updates come from a CDN release feed (index.json), NOT the GitHub API — so there
        // is no token in the client at all. CI in the private firmware repo publishes the artifacts +
        // feed to S3/CloudFront; the client only reads. See ReleaseFeedService / docs/ARCHITECTURE.md.
        private const string DefaultFirmwareFeed =
            "https://d11eqmwet07q29.cloudfront.net/firmware/index.json";

        /// <summary>The firmware feed URL — overridable via <c>VESPERAPP_FIRMWARE_FEED</c> so the
        /// CDN prefix can change without a rebuild.</summary>
        private static Uri FirmwareFeedUrl()
        {
            string? overrideUrl = Environment.GetEnvironmentVariable("VESPERAPP_FIRMWARE_FEED");
            return new Uri(string.IsNullOrWhiteSpace(overrideUrl) ? DefaultFirmwareFeed : overrideUrl);
        }

        public ICommand? RefreshReleasesCommand { get; }
        public ICommand? DownloadReleaseCommand { get; }
        public ObservableCollection<ReleaseEntry> Releases { get; } = new();
        private readonly ReleaseFeedService feedService;

        public SelectionModel<ReleaseEntry?>? SelectedFirmwareRelease { get; }


        public bool IsLoadingReleases
        {
            get => isLoadingReleases;
            set => this.RaiseAndSetIfChanged(ref isLoadingReleases, value);
        }

        private bool isLoadingReleases = false;



        public FirmwareUpgradesViewModel()
        {
            SelectedFirmwareRelease = new SelectionModel<ReleaseEntry?>()
            {
                SingleSelect = true,
            };
            Releases = new ObservableCollection<ReleaseEntry>();
            #region Updater Commands

            RefreshReleasesCommand = ReactiveCommand.CreateFromTask(RunReleasesRefresh);
            DownloadReleaseCommand = ReactiveCommand.CreateFromTask(RunReleaseDownload);

            #endregion

            feedService = new ReleaseFeedService(FirmwareFeedUrl());

            //Load the releases on initialization
            _ = RunReleasesRefresh();
        }



        private async Task<bool> RunReleasesRefresh()
        {
            try
            {
                IsLoadingReleases = true;
                Releases.Clear();
                var releases = await feedService.GetReleasesAsync();
                foreach (var release in releases)
                    Releases.Add(release);
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

        private async Task<bool> RunReleaseDownload()
        {
            var selected = SelectedFirmwareRelease?.SelectedItem;
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
