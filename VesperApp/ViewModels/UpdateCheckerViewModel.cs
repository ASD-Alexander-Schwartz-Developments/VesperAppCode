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

        private bool _isChecking;
        private bool _isUpdateAvailable;
        private bool _isPendingRestart;
        private bool _isUpdateDownloading;
        private string? _status;

        private readonly string _rid = "win-x64";
        private readonly string? _installedChannel;
        private UpdateManager? _um;
        private UpdateInfo? _update;
        private UpdateChannelPolicy.Decision _decision;
        private bool _canLeaveBeta;

        private Velopack.VelopackAsset? _latestRelease;
        public ObservableCollection<VelopackAsset> AvailableUpdates { get; } = new();

        public ICommand CheckUpdatesCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand RestartApplyCommand { get; }
        /// <summary>Explicit user action: switch this install from the beta ring to stable
        /// (the only path on which a lower version may be offered).</summary>
        public ICommand LeaveBetaCommand { get; }

        /// <summary>The ring this install follows: "stable" or "beta".</summary>
        public string InstalledRing => UpdateChannelPolicy.RingOf(_installedChannel);

        public string ChannelInfo => _um is null
            ? "Update channel: none (development build)"
            : $"Update channel: {_decision.Channel}";

        public bool CanLeaveBeta
        {
            get => _canLeaveBeta;
            private set => this.RaiseAndSetIfChanged(ref _canLeaveBeta, value);
        }

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
            CheckUpdatesCommand = ReactiveCommand.CreateFromTask(CheckForNormalUpdatesAsync);
            UpdateCommand = ReactiveCommand.CreateFromTask(PerformUpdateAsync, this.WhenAnyValue(x => x.IsUpdateAvailable));
            RestartApplyCommand = ReactiveCommand.Create(RestartApplyUpdate, this.WhenAnyValue(x => x.IsPendingRestart));

            if (Design.IsDesignMode == false && !string.IsNullOrWhiteSpace(_updateUrl))
            {
                // Runtime id of THIS OS + process architecture, so an x86 install doesn't
                // update itself into an x64 build (and vice versa). The app-release CI publishes
                // <rid>-stable and <rid>-beta channels for win-x64 / win-x86 (/ linux-x64).
                if (OperatingSystem.IsWindows())
                    _rid = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture ==
                          System.Runtime.InteropServices.Architecture.X86 ? "win-x86" : "win-x64";
                else if (OperatingSystem.IsMacOS())
                    _rid = "osx-arm64";
                else
                    _rid = "linux-x64";

                // The channel the installed package was packed for (vpk --channel), e.g.
                // win-x64-beta. Null in a dev build (not installed via Velopack).
                try
                {
                    if (Velopack.Locators.VelopackLocator.IsCurrentSet)
                        _installedChannel = Velopack.Locators.VelopackLocator.Current.Channel;
                }
                catch { _installedChannel = null; }

                // Normal checks follow the installed ring and never offer a lower version.
                _um = CreateManager(UpdateChannelPolicy.Decide(_rid, _installedChannel));
            }

            CanLeaveBeta = _um != null && InstalledRing == UpdateChannelPolicy.BetaRing;
            LeaveBetaCommand = ReactiveCommand.CreateFromTask(LeaveBetaAsync, this.WhenAnyValue(x => x.CanLeaveBeta));

            _update = null;
        }

        private UpdateManager CreateManager(UpdateChannelPolicy.Decision decision)
        {
            _decision = decision;
            this.RaisePropertyChanged(nameof(ChannelInfo));
            return new UpdateManager(_updateUrl, new UpdateOptions
            {
                // Velopack treats ExplicitChannel == installed channel as "no switch".
                ExplicitChannel = decision.Channel,
                AllowVersionDowngrade = decision.AllowDowngrade,
            });
        }

        /// <summary>"Leave beta": check the stable channel for this rid, allowing the (usually
        /// lower) stable version. After the update is applied the app is a stable install.</summary>
        private async Task LeaveBetaAsync()
        {
            if (_um is null)
                return;
            _um = CreateManager(UpdateChannelPolicy.Decide(_rid, _installedChannel, UpdateChannelPolicy.StableRing));
            await CheckForUpdatesAsync();
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

        /// <summary>"Check for updates": back to the installed ring if a ring switch was previewed.</summary>
        private Task CheckForNormalUpdatesAsync()
        {
            if (_um != null && _decision.IsRingSwitch)
                _um = CreateManager(UpdateChannelPolicy.Decide(_rid, _installedChannel));
            return CheckForUpdatesAsync();
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
                _update = await _um.CheckForUpdatesAsync();
                // Belt and braces: whatever the feed says, a normal check never offers a
                // version that is not newer than the running one.
                if (_update != null && !UpdateChannelPolicy.ShouldOffer(_um.CurrentVersion, _update.TargetFullRelease?.Version, _decision))
                    _update = null;
                if (_update != null)
                {
                    // DeltasToTarget may be empty when no delta chain reaches the target
                    // (client too far behind, old fulls purged) — the full release still
                    // constitutes an available update, so don't gate on deltas.
                    foreach (var rel in _update.DeltasToTarget)
                        AvailableUpdates.Add(rel);

                    _latestRelease = _update.TargetFullRelease;
                    IsUpdateAvailable = true;
                    Status = _decision.IsRingSwitch
                        ? $"Stable release available: {_latestRelease?.Version} (leaves the beta ring)"
                        : $"Update available: {_latestRelease?.Version}";
                }
                else
                {
                    IsUpdateAvailable = false;
                    Status = _decision.IsRingSwitch
                        ? "No stable release available for this platform."
                        : "No updates available.";
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