using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using ASD.Contracts;
using Avalonia.Threading;
using ReactiveUI;
using VesperApp.Services;

namespace VesperApp.ViewModels
{
    /// <summary>
    /// Drives the GNSS plugin-pack update page. Reads the pack feed from the CDN, downloads + stages
    /// the matching pack via <see cref="PluginUpdateService"/> (registration-gated, applied next
    /// launch), and offers a restart. No credentials — see docs/ARCHITECTURE.md.
    /// </summary>
    public class PluginUpdatesViewModel : ViewModelBase
    {
        // The GNSS pack feed lives on the client-read CDN origin (CdnConfig), path "plugins/gnss/index.json".
        // The origin is injected at build time — not a literal here — and is overridable per-asset via
        // VESPERAPP_GNSS_FEED (full URL) or wholesale via VESPERAPP_CDN_BASE. See docs/CDN-HARDENING.md.
        private const string GnssFeedPath = "plugins/gnss/index.json";

        private readonly PluginUpdateService? _service;
        private ReleaseEntry? _latest;

        public PluginUpdatesViewModel()
        {
            string? overrideUrl = Environment.GetEnvironmentVariable("VESPERAPP_GNSS_FEED");
            Uri? feed = !string.IsNullOrWhiteSpace(overrideUrl)
                ? new Uri(overrideUrl)
                : CdnConfig.FeedUri(GnssFeedPath);
            // The GNSS pack is download-gated to REGISTERED users (free OR paid), not to a paid
            // entitlement — so the service is left ungated here and the page gates on the session.
            // Real per-download enforcement is the backend signed-URL path (downloadUrlResolver),
            // which activates with sign-in; the feed is public-read until then. See docs/ci-templates/README.md.
            _service = feed is null
                ? null
                : new PluginUpdateService(feed, packName: "gnss", requiredEntitlement: null);

            // TODO(login): RE-ENABLE the registered-account gate once login/session functionality
            // is implemented. Temporarily bypassed so plugin downloads can be tested without a
            // signed-in session. Restore the line below (and remove the bypass) at that point:
            //     IsEntitled = _service is not null && AccessContext.IsRegistered;
            IsEntitled = _service is not null;   // TEMP: gate disabled for testing — see TODO(login) above

            CheckCommand = ReactiveCommand.CreateFromTask(CheckAsync,
                this.WhenAnyValue(x => x.IsChecking, x => x.IsDownloading, (c, d) => !c && !d));
            DownloadCommand = ReactiveCommand.CreateFromTask(DownloadAsync,
                this.WhenAnyValue(x => x.IsUpdateAvailable, x => x.IsReinstallAvailable, x => x.IsDownloading,
                    (upd, re, d) => (upd || re) && !d));
            RestartCommand = ReactiveCommand.Create(Relaunch, this.WhenAnyValue(x => x.IsPendingRestart));

            CurrentVersion = _service?.InstalledVersion() ?? NotInstalled;

            Status = _service is null
                ? "Plugin updates are unavailable: no update source is configured in this build."
                : IsEntitled
                    ? "Check for a GNSS decoder plugin update."
                    : "Sign in with a registered account (free or paid) to download the GNSS decoder plugin.";
        }

        public ICommand CheckCommand { get; }
        public ICommand DownloadCommand { get; }
        public ICommand RestartCommand { get; }

        private bool _isEntitled;
        public bool IsEntitled { get => _isEntitled; set => this.RaiseAndSetIfChanged(ref _isEntitled, value); }

        private bool _isChecking;
        public bool IsChecking { get => _isChecking; set => this.RaiseAndSetIfChanged(ref _isChecking, value); }

        private bool _isDownloading;
        public bool IsDownloading { get => _isDownloading; set => this.RaiseAndSetIfChanged(ref _isDownloading, value); }

        private const string NotInstalled = "not installed";

        private bool _isUpdateAvailable;
        public bool IsUpdateAvailable { get => _isUpdateAvailable; set => this.RaiseAndSetIfChanged(ref _isUpdateAvailable, value); }

        // A matching-or-older version is on the feed → offer a reinstall (e.g. to repair a corrupt pack).
        private bool _isReinstallAvailable;
        public bool IsReinstallAvailable { get => _isReinstallAvailable; set => this.RaiseAndSetIfChanged(ref _isReinstallAvailable, value); }

        private string _currentVersion = NotInstalled;
        public string CurrentVersion { get => _currentVersion; set => this.RaiseAndSetIfChanged(ref _currentVersion, value); }

        private bool _isPendingRestart;
        public bool IsPendingRestart { get => _isPendingRestart; set => this.RaiseAndSetIfChanged(ref _isPendingRestart, value); }

        private int _progress;
        public int Progress { get => _progress; set => this.RaiseAndSetIfChanged(ref _progress, value); }

        private string? _latestVersion;
        public string? LatestVersion { get => _latestVersion; set => this.RaiseAndSetIfChanged(ref _latestVersion, value); }

        private string? _status;
        public string? Status { get => _status; set => this.RaiseAndSetIfChanged(ref _status, value); }

        private async Task CheckAsync()
        {
            if (!IsEntitled || _service is null) return;
            try
            {
                IsChecking = true;
                IsUpdateAvailable = false;
                IsReinstallAvailable = false;
                Status = "Checking for GNSS plugin updates…";

                CurrentVersion = _service.InstalledVersion() ?? NotInstalled;   // refresh in case it changed
                _latest = await _service.CheckLatestAsync();
                if (_latest is null)
                {
                    Status = "No compatible GNSS plugin found for this platform.";
                }
                else
                {
                    LatestVersion = _latest.Version ?? "?";
                    string? installed = _service.InstalledVersion();
                    if (PluginUpdateService.IsNewer(_latest.Version, installed))
                    {
                        IsUpdateAvailable = true;
                        Status = $"Update available: {LatestVersion} (installed {CurrentVersion}).";
                    }
                    else
                    {
                        // Nothing newer — the latest on the feed is what's installed. Offer a reinstall.
                        IsReinstallAvailable = true;
                        Status = $"Up to date — {LatestVersion} is installed. You can reinstall it if the pack is damaged.";
                    }
                }
            }
            catch (Exception ex)
            {
                Status = "Check failed: " + ex.Message;
            }
            finally
            {
                IsChecking = false;
            }
        }

        private async Task DownloadAsync()
        {
            if (_latest is null || _service is null) return;
            try
            {
                IsDownloading = true;
                Progress = 0;
                bool reinstall = IsReinstallAvailable;
                Status = $"{(reinstall ? "Reinstalling" : "Downloading")} GNSS plugin {LatestVersion}…";
                var progress = new Progress<double>(p =>
                    Dispatcher.UIThread.Post(() => Progress = (int)Math.Round(p * 100)));

                var result = await _service.DownloadAndStageAsync(_latest, progress);
                Status = result.Message;
                IsPendingRestart = result.Success;
                if (result.Success)
                {
                    IsUpdateAvailable = false;
                    IsReinstallAvailable = false;
                    // The pack is now staged on disk, so its plugin.json already reads the new
                    // version — reflect that so the page no longer shows the old number.
                    CurrentVersion = _service.InstalledVersion() ?? LatestVersion ?? CurrentVersion;
                }
            }
            catch (Exception ex)
            {
                Status = "Download failed: " + ex.Message;
            }
            finally
            {
                IsDownloading = false;
            }
        }

        private void Relaunch()
        {
            try
            {
                string? exe = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exe))
                    Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            }
            catch { /* best effort */ }
            App.Shutdown();
        }
    }
}
