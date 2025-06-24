using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Velopack;
using Velopack.Sources;

namespace VesperApp.ViewModels
{
    public class UpdateCheckerViewModel : ViewModelBase
    {
        private readonly string _updateUrl = "https://d11eqmwet07q29.cloudfront.net";

        private bool _isChecking;
        private bool _isUpdateAvailable;
        private bool _isPendingRestart;
        private bool _isUpdateDownloading;
        private string? _status;

        private UpdateManager _um;
        private UpdateInfo? _update;

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

            _um = new UpdateManager(_updateUrl);
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
                    TextStatus.Text = $"Downloading ({percent}%)...";
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
                var mgr = new UpdateManager(new GithubSource("your-github-owner", "your-repo"));
                var updateInfo = await mgr.CheckForUpdatesAsync();
                if (updateInfo.ReleasesToApply.Count > 0)
                {
                    foreach (var rel in updateInfo.ReleasesToApply)
                        AvailableUpdates.Add(rel);

                    LatestRelease = updateInfo.FutureReleaseEntry;
                    IsUpdateAvailable = true;
                    Status = $"Update available: {LatestRelease?.Version}";
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
            if (LatestRelease == null)
                return;

            IsChecking = true;
            Status = "Downloading and applying update...";
            try
            {
                var mgr = new UpdateManager(new GithubSource("your-github-owner", "your-repo"));
                await mgr.DownloadUpdatesAsync();
                await mgr.ApplyUpdatesAsync();
                Status = "Update applied. Please restart the application.";
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

        private void Working()
        {
            Program.Log.LogInformation("");
            BtnCheckUpdate.IsEnabled = false;
            BtnDownloadUpdate.IsEnabled = false;
            BtnRestartApply.IsEnabled = false;
            TextStatus.Text = "Working...";
        }

    }
}