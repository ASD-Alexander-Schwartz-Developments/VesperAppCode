using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ASD.Contracts;
using Avalonia.Threading;
using ReactiveUI;
using VesperApp.Services;

namespace VesperApp.ViewModels
{
    /// <summary>One plugin pack the Plugins page offers: its folder under plugins/, its feed on the CDN
    /// and how the page describes it.</summary>
    public sealed record PluginPack(string PackName, string Title, string Description, string FeedPath, string FeedOverrideVariable);

    /// <summary>
    /// The Plugins page: one card per plugin pack. Each pack updates on its own channel; add a pack by
    /// adding it to <see cref="Catalog"/> (and, for a new kind of service, a contract in ASD.Contracts
    /// the plugin loader binds).
    /// </summary>
    public class PluginUpdatesViewModel : ViewModelBase
    {
        // Feeds live on the client-read CDN origin (CdnConfig) at plugins/<pack>/index.json. The origin is
        // injected at build time — not a literal here — and is overridable per pack (full URL) or wholesale
        // via VESPERAPP_CDN_BASE. See docs/CDN-HARDENING.md.
        public static IReadOnlyList<PluginPack> Catalog { get; } = new[]
        {
            new PluginPack("gnss", "GNSS Decoder",
                "Decodes VT04-VESPER GNSS snapshots into positions (GNSS Decoding, Validate Fix).",
                "plugins/gnss/index.json", "VESPERAPP_GNSS_FEED"),
            new PluginPack("pluto-gnss", "GNSS RF Test",
                "Drives a PlutoSDR bench transmitter for Device Tests ▸ GNSS / RF: tone self-test, simulated scenarios and station calibration. Shielded test setups only.",
                "plugins/pluto-gnss/index.json", "VESPERAPP_GNSS_RF_FEED"),
        };

        public PluginUpdatesViewModel()
        {
            Packs = Catalog.Select(p => new PluginPackViewModel(p)).ToList();
        }

        public IReadOnlyList<PluginPackViewModel> Packs { get; }
    }

    /// <summary>
    /// One plugin pack on the Plugins page. Reads the pack feed from the CDN, downloads + stages the
    /// matching pack via <see cref="PluginUpdateService"/> (registration-gated, applied next launch), and
    /// offers a restart. No credentials — see docs/ARCHITECTURE.md.
    /// </summary>
    public class PluginPackViewModel : ViewModelBase
    {
        private readonly PluginUpdateService? _service;
        private ReleaseEntry? _latest;

        public PluginPackViewModel(PluginPack pack)
        {
            Title = pack.Title;
            Description = pack.Description;
            string? overrideUrl = Environment.GetEnvironmentVariable(pack.FeedOverrideVariable);
            Uri? feed = !string.IsNullOrWhiteSpace(overrideUrl)
                ? new Uri(overrideUrl)
                : CdnConfig.FeedUri(pack.FeedPath);
            // Packs are download-gated to REGISTERED users (free OR paid), not to a paid
            // entitlement — so the service is left ungated here and the page gates on the session.
            // Real per-download enforcement is the backend signed-URL path (downloadUrlResolver),
            // which activates with sign-in; the feed is public-read until then. See docs/ci-templates/README.md.
            _service = feed is null
                ? null
                : new PluginUpdateService(feed, packName: pack.PackName, requiredEntitlement: null);

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
            string? installedProblem = null;
            try { installedProblem = _service?.InstalledPackProblem(); } catch { /* shown on Check */ }

            Status = _service is null
                ? "Plugin updates are unavailable: no update source is configured in this build."
                : IsEntitled
                    ? "Check for an update."
                    : "Sign in with a registered account (free or paid) to download this plugin.";
            if (_service is not null && installedProblem != null)
                Status = installedProblem + " " + Status;
        }

        public string Title { get; }
        public string Description { get; }

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
                Status = "Checking for updates…";

                CurrentVersion = _service.InstalledVersion() ?? NotInstalled;   // refresh in case it changed
                PluginFeedCheck feed = await _service.CheckFeedAsync();
                _latest = feed.Installable;
                string? installedProblem = _service.InstalledPackProblem();
                if (_latest is null)
                {
                    // Feed entries that would install but never load (no adapter / no or foreign ABI,
                    // e.g. pluto-gnss 0.1.0) are not offered — say so instead of offering a dead pack.
                    Status = feed.Newest is null
                        ? "No version of this plugin is published for this platform yet."
                        : $"Version {feed.Newest.Version} is published, but it has no plugin adapter this VesperApp can load — it is not offered. Wait for a newer pack.";
                    if (installedProblem != null)
                        Status += " " + installedProblem;
                }
                else if (installedProblem != null)
                {
                    // The installed pack can't load; offer the feed version as a repair whatever its number.
                    LatestVersion = _latest.Version ?? "?";
                    if (PluginUpdateService.IsNewer(_latest.Version, _service.InstalledVersion()))
                        IsUpdateAvailable = true;
                    else
                        IsReinstallAvailable = true;
                    Status = $"{installedProblem} Install {LatestVersion} to fix it.";
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
                Status = $"{(reinstall ? "Reinstalling" : "Downloading")} {Title} {LatestVersion}…";
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
