using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using VesperApp.Services;

namespace VesperApp.ViewModels
{
    /// <summary>
    /// Backs the Help tab. Navigation and page content come live from the project's GitHub wiki
    /// via <see cref="WikiHelpService"/>; when the wiki is unreachable the service serves the last
    /// fetched copy (or the embedded one), and this view model surfaces that as an offline banner.
    /// </summary>
    public sealed class HelpViewModel : ViewModelBase
    {
        private readonly WikiHelpService _wiki = new();
        private CancellationTokenSource? _pageLoadCts;

        public HelpViewModel()
        {
            RefreshCommand = ReactiveCommand.CreateFromTask(LoadAsync);
            OpenInBrowserCommand = ReactiveCommand.Create(OpenInBrowser);
            _ = LoadAsync();
        }

        public ObservableCollection<HelpNavItem> Pages { get; } = new();

        private HelpNavItem? _selectedPage;
        public HelpNavItem? SelectedPage
        {
            get => _selectedPage;
            set
            {
                if (_selectedPage == value) return;
                this.RaiseAndSetIfChanged(ref _selectedPage, value);
                if (value?.PageName is { } page)
                    _ = LoadPageAsync(page);
            }
        }

        private string _pageMarkdown = string.Empty;
        public string PageMarkdown
        {
            get => _pageMarkdown;
            private set => this.RaiseAndSetIfChanged(ref _pageMarkdown, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
        }

        private string _banner = string.Empty;
        public string Banner
        {
            get => _banner;
            private set
            {
                this.RaiseAndSetIfChanged(ref _banner, value);
                this.RaisePropertyChanged(nameof(HasBanner));
            }
        }

        public bool HasBanner => !string.IsNullOrEmpty(Banner);

        public ICommand RefreshCommand { get; }
        public ICommand OpenInBrowserCommand { get; }

        /// <summary>Follow an in-wiki link (e.g. "Getting-Started") from the rendered page.</summary>
        public void NavigateToPage(string pageName)
        {
            HelpNavItem? item = Pages.FirstOrDefault(p =>
                string.Equals(p.PageName, pageName, StringComparison.OrdinalIgnoreCase));

            if (item is not null)
                SelectedPage = item;          // triggers the load
            else
                _ = LoadPageAsync(pageName);  // page exists on the wiki but not in the sidebar
        }

        private async Task LoadAsync()
        {
            IsBusy = true;
            try
            {
                string? keepPage = SelectedPage?.PageName;
                var nav = await _wiki.GetNavigationAsync();

                Pages.Clear();
                foreach (var item in nav)
                    Pages.Add(item);

                HelpNavItem? target =
                    Pages.FirstOrDefault(p => p.IsPage && string.Equals(p.PageName, keepPage, StringComparison.OrdinalIgnoreCase))
                    ?? Pages.FirstOrDefault(p => p.IsPage);

                if (target is not null)
                {
                    if (ReferenceEquals(target, SelectedPage) && target.PageName is { } page)
                        await LoadPageAsync(page);   // refresh re-fetches the current page
                    else
                        SelectedPage = target;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadPageAsync(string pageName)
        {
            _pageLoadCts?.Cancel();
            var cts = _pageLoadCts = new CancellationTokenSource();

            IsBusy = true;
            try
            {
                HelpPage? page = await _wiki.GetPageAsync(pageName, cts.Token);
                if (cts.IsCancellationRequested) return;

                if (page is null)
                {
                    PageMarkdown =
                        $"# Page unavailable\n\nCouldn't load **{pageName}** — the wiki is unreachable " +
                        "and no offline copy exists yet.\n\nCheck your internet connection and press **Refresh**, " +
                        $"or open the documentation in a browser: {WikiHelpService.WikiHomeUrl}";
                    Banner = string.Empty;
                }
                else
                {
                    PageMarkdown = page.Markdown;
                    Banner = page.FromCache
                        ? "Offline copy — the wiki couldn't be reached, so this may not be the latest version."
                        : string.Empty;

                    // Pull any images this page references into the local cache; when new ones
                    // arrive, re-render so they appear (cached pages render them instantly).
                    if (await _wiki.PrefetchImagesAsync(page.Markdown, cts.Token) &&
                        !cts.IsCancellationRequested)
                    {
                        string md = PageMarkdown;
                        PageMarkdown = string.Empty;
                        PageMarkdown = md;
                    }
                }
            }
            catch (OperationCanceledException) { /* superseded by a newer navigation */ }
            finally
            {
                if (ReferenceEquals(_pageLoadCts, cts))
                    IsBusy = false;
            }
        }

        private static void OpenInBrowser()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = WikiHelpService.WikiHomeUrl.ToString(),
                    UseShellExecute = true,
                });
            }
            catch (Exception e) { Debug.WriteLine(e); }
        }
    }
}
