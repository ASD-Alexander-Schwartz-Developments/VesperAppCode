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


/*
 * 
 *  rem win-x64-stable
    rem win-x64-beta
    rem osx-arm64-stable
    rem osx-arm64-beta
 */

namespace VesperApp.ViewModels
{
    public class UpdateCheckerViewModel : ViewModelBase
    {
        private readonly string _updateUrl = "https://d11eqmwet07q29.cloudfront.net";
        private readonly string updateFileName = "VesperAppSetup.msi";
        private readonly string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private readonly List<string> _channels;

        private bool _isChecking;
        private bool _isUpdateAvailable;
        private bool _isPendingRestart;
        private bool _isUpdateDownloading;
        private string? _status;

        private UpdateManager _um;
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

            if(OperatingSystem.IsWindows())
            {
                _channels = new List<string>
                {
                    "win-x64-stable",
                    "win-x64-beta",
                };
            }
            else if(OperatingSystem.IsMacOS())
            {
                _channels = new List<string>
                {
                    "osx-arm64-stable",
                    "osx-arm64-beta"
                };
            }
            else
            {
                _channels = new List<string>();
            }

            _um = new UpdateManager(_updateUrl, new UpdateOptions
            {
                ExplicitChannel = "win-x64-stable",
                AllowVersionDowngrade = true,
            });

            _update = null;
        }

        private void RestartApplyUpdate()
        {
            if(_update != null)
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

            if (_update != null)
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

        private void Working()
        {
            Program.Log.LogInformation("");
            IsChecking = false;
            IsUpdateDownloading = false;
            IsPendingRestart = false;
            Status = "Working...";
        }

    }
}