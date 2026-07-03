using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Velopack;
using Velopack.Sources;
using VesperApp.Services;

namespace VesperApp.ViewModels
{
    public class UpdateCheckerViewModel : ViewModelBase
    {
        // Velopack self-update feed root — the client-read CDN origin, injected at build time
        // (CdnConfig / [AssemblyMetadata]) and overridable via VESPERAPP_CDN_BASE. Empty in a dev
        // build with no origin configured, in which case update checks are disabled gracefully.
        private readonly string _updateUrl = CdnConfig.BaseUrl;
        private readonly string updateFileName = "VesperAppSetup.msi";
        private readonly string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private readonly List<string> _channels;

        private bool _isChecking;
        private bool _isUpdateAvailable;
        private bool _isPendingRestart;
        private bool _isUpdateDownloading;
        private string? _status;

        private UpdateManager? _um;
        private UpdateInfo? _update;

        private Velopack.VelopackAsset? _latestRelease;
        public ObservableCollection<VelopackAsset> AvailableUpdates { get; } = new();

        public ICommand CheckUpdatesCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand RestartApplyCommand { get; }

        public bool IsChecking
        {
            get => _isChecking;
            set => this.RaiseAndSetIfChanged(ref _isChecking, value);
        }

        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            set => this.RaiseAndSetIfChanged(ref _isUpdateAvailable, value);
        }
        public bool IsUpdateDownloading
        {
            get => _isUpdateDownloading;
            set => this.RaiseAndSetIfChanged(ref _isUpdateDownloading, value);
        }

        public bool IsPendingRestart
        {
            get => _isPendingRestart;
            set => this.RaiseAndSetIfChanged(ref _isPendingRestart, value);
        }

        public string? Status
        {
            get => _status;
            set => this.RaiseAndSetIfChanged(ref _status, value);
        }

        public UpdateCheckerViewModel()
        {
            CheckUpdatesCommand = ReactiveCommand.CreateFromTask(CheckForUpdatesAsync);
            UpdateCommand = ReactiveCommand.CreateFromTask(PerformUpdateAsync, this.WhenAnyValue(x => x.IsUpdateAvailable));
            RestartApplyCommand = ReactiveCommand.Create(RestartApplyUpdate, this.WhenAnyValue(x => x.IsPendingRestart));

            if (Design.IsDesignMode == false && !string.IsNullOrWhiteSpace(_updateUrl))
            {
                // Pick the auto-update channel for THIS OS. Previously the channel was hardcoded to
                // win-x64-stable, so non-Windows hosts polled the wrong feed and Linux had none —
                // the app-release CI now publishes win-x64 and linux-x64 channels to the same root.
                string stable, beta;
                if (OperatingSystem.IsWindows())    { stable = "win-x64-stable";   beta = "win-x64-beta"; }
                else if (OperatingSystem.IsMacOS()) { stable = "osx-arm64-stable"; beta = "osx-arm64-beta"; }
                else                                { stable = "linux-x64-stable"; beta = "linux-x64-beta"; }

                _channels = new List<string> { stable, beta };

                _um = new UpdateManager(_updateUrl, new UpdateOptions
                {
                    ExplicitChannel = stable,
                    AllowVersionDowngrade = true,
                });
            }

            _update = null;
        }

        private void RestartApplyUpdate()
        {
            if(_update != null && _um != null)
            {
                _um.ApplyUpdatesAndRestart(_update);
            }
        }

        private void Progress(int percent)
        {
            // progress can be sent from other threads
            Dispatcher.UIThread.InvokeAsync(
                () => {
                    Status = $"Downloading ({percent}%)...";
                });
        }

        private async Task CheckForUpdatesAsync()
        {
            if (_um is null)
            {
                Status = "Updates unavailable: no update source is configured in this build.";
                return;
            }

            IsChecking = true;
            Status = "Checking for updates...";
            AvailableUpdates.Clear();
            try
            {
                // Adjust the source as needed (e.g., GithubSource, FileSource, etc.)
                _update = await _um.CheckForUpdatesAsync();
                if (_update?.DeltasToTarget.Length > 0)
                {
                    foreach (var rel in _update.DeltasToTarget)
                        AvailableUpdates.Add(rel);

                    _latestRelease = _update.TargetFullRelease;
                    IsUpdateAvailable = true;
                    Status = $"Update available: {_latestRelease?.Version}";
                }
                else
                {
                    IsUpdateAvailable = false;
                    Status = "No updates available.";
                }
            }
            catch (System.Exception ex)
            {
                Status = $"Error: {ex.Message}";
            }
            finally
            {
                IsChecking = false;
            }
        }

        private async Task PerformUpdateAsync()
        {
            if (_latestRelease == null)
                return;

            if (_update != null && _um != null)
            {
                IsChecking = true;
                Status = "Downloading and applying update...";
                try
                {
                    await _um.DownloadUpdatesAsync(_update, Progress).ConfigureAwait(true);
                    Status = "Update Downloaded. Restarting the application to apply updates.";
                    IsPendingRestart = true;
                }
                catch (System.Exception ex)
                {
                    Status = $"Update failed: {ex.Message}";
                }
                finally
                {
                    IsChecking = false;
                }
            }
            else
            {
                Status = "No update available to apply.";
            }
        }

    }
}