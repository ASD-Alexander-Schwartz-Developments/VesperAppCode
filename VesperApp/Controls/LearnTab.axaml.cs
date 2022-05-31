using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using VesperApp.Services;


namespace VesperApp.Controls {
    public partial class LearnTab: UserControl {
        public LearnTab() => AvaloniaXamlLoader.Load(this);

        public void Docs() => UrlHelper.URL("https://github.com/mat1jaczyyy/apollo-studio/wiki");

        public void Tutorials() => UrlHelper.URL("https://www.youtube.com/playlist?list=PLKC4R3X00beY0aB_f_ZIa3shqJX7do4mH");

        public void Bug() => UrlHelper.URL("https://github.com/mat1jaczyyy/apollo-studio/issues/new?assignees=mat1jaczyyy&labels=bug&template=bug_report.md&title=");

        public void Feature() => UrlHelper.URL("https://github.com/mat1jaczyyy/apollo-studio/issues/new?assignees=mat1jaczyyy&labels=enhancement&template=feature_request.md&title=");

        public void Question() => UrlHelper.URL("https://github.com/mat1jaczyyy/apollo-studio/issues/new?assignees=mat1jaczyyy&labels=question&template=question.md&title=");

        public void Discord() => UrlHelper.URL("https://discordapp.com/invite/2ZSHYHA");

        public void Website() => UrlHelper.URL("https://apollo.mat1jaczyyy.com");

        //void Patron() => UrlHelper.URL(Patreon.URL);
    }
}
