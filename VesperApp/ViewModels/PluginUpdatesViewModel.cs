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
    /// the matching pack via <see cref="PluginUpdateService"/> (entitlement-gated, applied next
    /// launch), and offers a restart. No credentials — see docs/ARCHITECTURE.md.
    /// </summary>
    public class PluginUpdatesViewModel : ViewModelBase
    {
        private const string DefaultGnssFeed =
            "https://d11eqmwet07q29.cloudfront.net/plugins/gnss/index.json";

        private readonly PluginUpdateService _service;
        private ReleaseEntry? _latest;

        public PluginUpdatesViewModel()
        {
            string? overrideUrl = Environment.GetEnvironmentVariable("VESPERAPP_GNSS_FEED");
            var feed = new Uri(string.IsNullOrWhiteSpace(overrideUrl) ? DefaultGnssFeed : overrideUrl);
            _service = new PluginUpdateService(feed, packName: "gnss",
                requiredEntitlement: Entitlements.GnssPostProcessing);

            IsEntitled = _service.IsEntitled;

            CheckCommand = ReactiveCommand.CreateFromTask(CheckAsync,
                this.WhenAnyValue(x => x.IsChecking, x => x.IsDownloading, (c, d) => !c && !d));
            DownloadCommand = ReactiveCommand.CreateFromTask(DownloadAsync,
                this.WhenAnyValue(x => x.IsUpdateAvailable, x => x.IsDownloading, (a, d) => a && !d));
            RestartCommand = ReactiveCommand.Create(Relaunch, this.WhenAnyValue(x => x.IsPendingRestart));

            Status = IsEntitled
                ? "Check for a GNSS decoder plugin update."
                : "This account is not entitled to the GNSS decoder plugin.";
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

        private bool _isUpdateAvailable;
        public bool IsUpdateAvailable { get => _isUpdateAvailable; set => this.RaiseAndSetIfChanged(ref _isUpdateAvailable, value); }

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
            if (!IsEntitled) return;
            try
            {
                IsChecking = true;
                IsUpdateAvailable = false;
                Status = "Checking for GNSS plugin updates…";
                _latest = await _service.CheckLatestAsync();
                if (_latest is null)
                {
                    Status = "No compatible GNSS plugin found for this platform.";
                }
                else
                {
                    LatestVersion = _latest.Version ?? "?";
                    IsUpdateAvailable = true;
                    Status = $"Available: GNSS plugin {LatestVersion}.";
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
            if (_latest is null) return;
            try
            {
                IsDownloading = true;
                Progress = 0;
                Status = $"Downloading GNSS plugin {LatestVersion}…";
                var progress = new Progress<double>(p =>
                    Dispatcher.UIThread.Post(() => Progress = (int)Math.Round(p * 100)));

                var result = await _service.DownloadAndStageAsync(_latest, progress);
                Status = result.Message;
                IsPendingRestart = result.Success;
                if (result.Success)
                    IsUpdateAvailable = false;
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
