using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;



namespace VesperApp.Controls {
    public partial class LaunchpadButton: UserControl {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            canvas = this.Get<Canvas>("Canvas");
            path = this.Get<Path>("Path");
        }

        Canvas canvas;
        Path path;
        int Index;

        void AddClass(string name) {
            canvas.Classes.Add(name);
            path.Classes.Add(name);

            if (name == "corner")
                path.Data = (StreamGeometry)Application.Current.FindResource($"LPGrid_{Index}CornerGeometry");
        }

        public bool Empty => Canvas.Classes.Contains("empty");

        bool IsPhantom() {
            /*if (Preferences.LaunchpadModel.HasNovationLED() && Index == 9) return false;

            if (Preferences.LaunchpadStyle == LaunchpadStyles.Stock) {
                int x = Index % 10;
                int y = Index / 10;

                if (x == 0 || x == 9 || y == 0 || y == 9) return true;
            }

            return Preferences.LaunchpadStyle == LaunchpadStyles.Phantom;*/
            return false;
        }
        
        public int UpdateModel() {
            int x = Index % 10;
            int y = Index / 10;

            int ret = 0;
            if (!Empty) ret--;

            canvas.Classes.Clear();
            path.Classes.Clear();
            /*
            switch (Preferences.LaunchpadModel) {
                case LaunchpadModels.MK2:
                    if (x == 0 || y == 9 || Index == 9) AddClass("empty");
                    else {
                        ret++;

                        if (x == 9 || y == 0) AddClass("circle");
                        else if (Index == 44 || Index == 45 || Index == 54 || Index == 55) AddClass("corner");
                        else AddClass("square");
                    }
                    break;

                case LaunchpadModels.Pro:
                    if (Index == 0 || Index == 9 || Index == 90 || Index == 99) AddClass("empty");
                    else {
                        ret++;

                        if (x == 0 || x == 9 || y == 0 || y == 9) AddClass("circle");
                        else if (Index == 44 || Index == 45 || Index == 54 || Index == 55) AddClass("corner");
                        else AddClass("square");
                    }
                    break;

                case LaunchpadModels.X:
                    if (x == 0 || y == 9) AddClass("empty");
                    else {
                        ret++;

                        if (Index == 9) AddClass("novation");
                        else if (Index == 44 || Index == 45 || Index == 54 || Index == 55) AddClass("corner");
                        else AddClass("square");
                    }
                    break;

                case LaunchpadModels.ProMK3:
                    if (Index == 90 || Index == 99) AddClass("empty");
                    else {
                        ret++;

                        if (Index == 0) AddClass("hidden");
                        else if (Index == 9) AddClass("novation");
                        else if (Index == 44 || Index == 45 || Index == 54 || Index == 55) AddClass("corner");
                        else if (y == 9) AddClass("split");
                        else AddClass("square");
                    }
                    break;

                case LaunchpadModels.All:
                    ret++;

                    if (Index == 0 || Index == 9 || Index == 90 || Index == 99) AddClass("hidden");
                    else if (Index == 44 || Index == 45 || Index == 54 || Index == 55) AddClass("corner");
                    else AddClass("square");
                    break;
            }
            */
            ret++;

            if (Index == 0 || Index == 9 || Index == 90 || Index == 99) AddClass("hidden");
            else if (Index == 44 || Index == 45 || Index == 54 || Index == 55) AddClass("corner");
            else AddClass("square");
            return ret;
        }

        public void UpdateStyle()
            => path.Fill = IsPhantom()? SolidColorBrush.Parse("Transparent") : path.Stroke;

        public void SetColor(SolidColorBrush color)
            => path.Stroke = IsPhantom()? color : path.Fill = color;

        public LaunchpadButton() => throw new InvalidOperationException();

        public LaunchpadButton(int index) {
            InitializeComponent();

            Index = index;

            Grid.SetRow(this, Index / 10);
            Grid.SetColumn(this, Index % 10);

            AddClass("empty");
        }
    }
}
