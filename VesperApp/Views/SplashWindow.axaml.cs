using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using VesperApp.Controls;
using VesperApp.Services;

namespace VesperApp.Views
{
    public partial class SplashWindow : Window
    {
        static Image SplashImage = (Image)Application.Current.FindResource("SplashImage");
        public SplashWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

           // UpdateTopmost(Preferences.AlwaysOnTop);
            //Preferences.AlwaysOnTopChanged += UpdateTopmost;

            //Preferences.RecentsCleared += Clear;

            //observable = TabControl.get GetObservable(SelectingItemsControl.SelectedIndexProperty).Subscribe(TabChanged);

            //this.AddHandler(DragDrop.DropEvent, Drop);
            //this.AddHandler(DragDrop.DragOverEvent, DragOver);

            //this.Get<PreferencesButton>("PreferencesButton").HoleFill = Background;

            root.Children.Add(SplashImage);
            /*
            if (Program.HadCrashed)
                if (File.Exists(Program.CrashProject))
                {
                    CrashPanel.Opacity = 1;
                    CrashPanel.IsHitTestVisible = true;
                    CrashPanel.ZIndex = 1;

                }
                else ResolveCrash();
            */
        }


        IDisposable observable;

        Grid root, crashPanel;
        TabControl tabControl;
        StackPanel recents;
        TextBlock releaseVersion, releaseBody, releaseLink;
        UpdateButton updateButton;
        Button ignoreButton;

        void UpdateTopmost(bool value) => Topmost = value;


        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            root = this.Get<Grid>("Root");

            crashPanel = this.Get<Grid>("CrashPanel");
            ignoreButton = this.Get<Button>("IgnoreButton");

            tabControl = this.Get<TabControl>("TabControl");
            recents = this.Get<StackPanel>("Recents");

            releaseVersion = this.Get<TextBlock>("ReleaseVersion");
            releaseBody = this.Get<TextBlock>("ReleaseBody");
            releaseLink = this.Get<TextBlock>("ReleaseLink");

            updateButton = this.Get<UpdateButton>("UpdateButton");

        }


        //void UpdateTopmost(bool value) => Topmost = value;

        async void UpdateBlogpost()
        {
            /*
            Octokit.RepositoryContent latest;

            try
            {
                latest = await Github.LatestBlogpost();
            }
            catch
            {
                BlogpostBody.Text = "Failed to fetch blogpost data from GitHub.";
                return;
            }

            BlogpostBody.Text = $"{latest.Content.Replace("\r", "").Split('\n').First().Replace("# ", "").Replace("#", "")}\n" +
                $" published {DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(Path.GetFileNameWithoutExtension(latest.Name))).Humanize()}";

            BlogpostLink.Opacity = 1;
            BlogpostLink.IsHitTestVisible = true;*/
        }


        async void Open(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                AllowMultiple = false,
                Filters = new List<FileDialogFilter>() {
                    new FileDialogFilter() {
                        Extensions = new List<string>() {
                            "approj"
                        },
                        Name = "Apollo Project"
                    }
                },
                Title = "Open Project"
            };

            string[] result = await ofd.ShowAsync(this);

           // if (result.Length > 0)
           //     ReadFile(result[0]);
        }
        async void Blogpost(object sender, PointerReleasedEventArgs e) => 
            UrlHelper.URL("https://asd-tech.com"/*$"https://apollo.mat1jaczyyy.com/post/{Path.GetFileNameWithoutExtension((await Github.LatestBlogpost()).Name)}"*/);

        async void Release(object sender, PointerReleasedEventArgs e)
            => UrlHelper.URL(/*(await Github.LatestRelease()).HtmlUrl*/"https://google.com");


        async void UpdateRelease()
        {/*
            Octokit.Release latest;

            try
            {
                latest = await Github.LatestRelease();
            }
            catch
            {
                ReleaseBody.Text = "Failed to fetch release data from GitHub.";
                return;
            }

            ReleaseVersion.Text = $"{latest.Name} - published {latest.PublishedAt.Humanize()}";
            ReleaseBody.Text = String.Join('\n', latest.Body.Replace("\r", "").Split('\n').SkipWhile(i => i.Trim() == "Changes:" || i.Trim() == "").Take(3));
            ReleaseLink.Opacity = 1;
            ReleaseLink.IsHitTestVisible = true;*/
        }


        void TabChanged(int tab)
        {
/*            if (tab == 0)
            {
                for (int i = 0; i < Preferences.Recents.Count; i++)
                {
                    RecentProjectInfo viewer = new RecentProjectInfo(Preferences.Recents[i]);
                    viewer.Opened += ReadFile;
                    viewer.Removed += Remove;
                    viewer.Showed += App.URL;

                    Recents.Children.Add(viewer);
                }

            }
            else*/ recents.Children.Clear();
        }

        void New(object sender, RoutedEventArgs e)
        {
            //Program.Project?.Dispose();
            //Program.Project = new Project();

            //ProjectWindow.Create(this);
            Close();
        }


        void Restore(object sender, RoutedEventArgs e)
        {
            crashPanel.Opacity = 0;
            crashPanel.IsHitTestVisible = false;
            crashPanel.ZIndex = -1;

            //string originalPath = Preferences.CrashPath;

   //         ReadFile(Program.CrashProject, true);

//            if (Program.Project != null)
//                Program.Project.FilePath = originalPath;

 //           ResolveCrash();
        }

        void Loaded(object sender, EventArgs e)
        {
            Position = new PixelPoint(Position.X, Math.Max(0, Position.Y));

            //Launchpad.DisplayWarnings(this);


            UpdateBlogpost();
            UpdateRelease();

//            if (!Program.HadCrashed) CheckUpdate();
        }

        void Unloaded(object sender, CancelEventArgs e)
        {
            root.Children.Remove(SplashImage);

            //preferences.AlwaysOnTopChanged -= UpdateTopmost;
            //preferences.RecentsCleared += Clear;

            observable.Dispose();

            this.Content = null;

            App.WindowClosed(this);
        }

        void Update()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;

            //if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) Program.LaunchAdmin = true;
            //else UpdateWindow.Create(this);

            //foreach (Window window in App.Windows)
            //    if (window.GetType() != typeof(MessageWindow) && window.GetType() != typeof(UpdateWindow))
            //        window.Close();
        }

        void MoveWindow(object sender, PointerPressedEventArgs e) => BeginMoveDrag(e);

        void Minimize() => WindowState = WindowState.Minimized;


        void Ignore(object sender, RoutedEventArgs e)
        {
        }

        public static void Create(Window owner)
        {
            SplashWindow window = new SplashWindow();

            if (owner == null || owner.WindowState != WindowState.Minimized)
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.Owner = owner;
            }

            window.Show();
            window.Owner = null;

            window.Topmost = true;
            //window.Topmost = Preferences.AlwaysOnTop;
        }

    }
}
