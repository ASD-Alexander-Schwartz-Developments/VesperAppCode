using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using VesperApp.Services;
using VesperApp.ViewModels;

namespace VesperApp.Controls
{
    public partial class HelpView : UserControl
    {
        public HelpView()
        {
            InitializeComponent();

            if (MdViewer.Engine is global::Markdown.Avalonia.IMarkdownEngine engine)
            {
                // Route hyperlink clicks ourselves: in-wiki links ("Getting-Started") navigate
                // inside the Help tab; absolute http(s) links open the system browser.
                engine.HyperlinkCommand = new HelpLinkCommand(this);

                // Pages reference images relatively ("images/foo.png"); serve them from the
                // help cache / embedded copy so they work identically online and offline.
                engine.BitmapLoader = new WikiBitmapLoader();
            }
        }

        private sealed class WikiBitmapLoader : global::Markdown.Avalonia.Utils.IBitmapLoader
        {
            public string AssetPathRoot { set { /* resolution is handled by WikiHelpService */ } }

            public Bitmap? Get(string urlTxt)
            {
                try
                {
                    using Stream? s = WikiHelpService.OpenLocalImage(urlTxt);
                    return s is null ? null : new Bitmap(s);
                }
                catch
                {
                    return null;
                }
            }
        }

        private sealed class HelpLinkCommand : ICommand
        {
            private readonly HelpView _owner;

            public HelpLinkCommand(HelpView owner) => _owner = owner;

            public bool CanExecute(object? parameter) => true;

            public event EventHandler? CanExecuteChanged { add { } remove { } }

            public void Execute(object? parameter)
            {
                string? url = parameter?.ToString();
                if (string.IsNullOrWhiteSpace(url))
                    return;

                if (Uri.TryCreate(url, UriKind.Absolute, out Uri? abs) &&
                    (abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                    }
                    catch (Exception e) { Debug.WriteLine(e); }
                    return;
                }

                // Relative target → wiki page name (strip "./" prefixes and "#anchor" suffixes).
                string page = url!.TrimStart('.', '/');
                int hash = page.IndexOf('#');
                if (hash >= 0) page = page[..hash];

                if (page.Length > 0 && _owner.DataContext is HelpViewModel vm)
                    vm.NavigateToPage(page);
            }
        }
    }
}
