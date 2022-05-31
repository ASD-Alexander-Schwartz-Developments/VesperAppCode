using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Threading;
using VesperApp.Models;

namespace VesperApp.Controls {
    public partial class LaunchpadGrid: UserControl {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            root = this.Get<LayoutTransformControl>("Root");
            view = this.Get<Border>("View");
            back = this.Get<Border>("Back");

            modeLight = this.Get<Rectangle>("ModeLight");
        }
        
        LayoutTransformControl root;
        Border view, back;
        Grid grid;
        LaunchpadButton[] buttons;
        Rectangle modeLight;

        public delegate void PadChangedEventHandler(int index);
        public event PadChangedEventHandler PadStarted;
        public event PadChangedEventHandler PadFinished;
        public event PadChangedEventHandler PadPressed;
        public event PadChangedEventHandler PadReleased;

        public delegate void PadModsChangedEventHandler(int index, KeyModifiers mods);
        public event PadModsChangedEventHandler PadModsPressed;

        public static int GridToSignal(int index) => (index == -1)? 100 : ((9 - (index / 10)) * 10 + index % 10);
        public static int SignalToGrid(int index) => (index == 100)? -1 : ((9 - (index / 10)) * 10 + index % 10);

        public void SetColor(int index, SolidColorBrush color) {
            if (index == -1) {
                if (IsArrangeValid) ModeLight.Fill = color;
                else this.Resources["ModeBrush"] = color;

            } else buttons[index].SetColor(color);
        }

        public void RawUpdate(RawUpdate n) => Dispatcher.UIThread.InvokeAsync(() => {
            SetColor(LaunchpadGrid.SignalToGrid(n.Index), n.Color.ToScreenBrush());
        });

        public void Clear() {
            SolidColorBrush color = (SolidColorBrush)Application.Current.FindResource("ThemeForegroundLowBrush");
            for (int i = -1; i < 100; i++) SetColor(i, color);
        }

        void Update_LaunchpadStyle() {
            for (int i = 0; i < 100; i++)
                buttons[i].UpdateStyle();
        }

        void Update_LaunchpadModel() {
            grid?.Children.Clear();

            int nbuttons = 0;//Preferences.LaunchpadModel.GridSize();

            IEnumerable<string> ones = Enumerable.Range(0, nbuttons).Select(i => "*");
            IEnumerable<string> zeros = Enumerable.Range(0, 10 - nbuttons).Select(i => "0");

            View.Child = grid = new Grid() {
                RowDefinitions = RowDefinitions.Parse(
                    String.Join(
                        ",",
                        ones.Concat(zeros).ToArray()
                    )
                ),
                ColumnDefinitions = ColumnDefinitions.Parse(
                    String.Join(
                        ",",
                        zeros.Concat(ones).ToArray()
                    )
                )
            };

            for (int i = 0; i < 100; i++) {
                int mouse = buttons[i].UpdateModel();

                if (mouse < 0) buttons[i].PointerPressed -= MouseDown;
                else if (mouse > 0) buttons[i].PointerPressed += MouseDown;

                grid.Children.Add(buttons[i]);
            }

            modeLight.IsVisible = true;//Preferences.LaunchpadModel.HasModeLight();

            Update_LaunchpadStyle();
        }

        void Update_LaunchpadRotation()
            => Root.LayoutTransform = new RotateTransform(/*Preferences.LaunchpadGridRotation? -45.0 :*/ 0.0);

        public LaunchpadGrid() {
            InitializeComponent();

            buttons = new LaunchpadButton[100];

            for (int i = 0; i < 100; i++)
                buttons[i] = new LaunchpadButton(i);

            //Preferences.LaunchpadModelChanged += Update_LaunchpadModel;
            //Preferences.LaunchpadStyleChanged += Update_LaunchpadStyle;
            //Preferences.LaunchpadGridRotationChanged += Update_LaunchpadRotation;

            Update_LaunchpadModel();
            Update_LaunchpadRotation();
            
            Clear();
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) {
            PadStarted = null;
            PadFinished = null;
            PadPressed = null;
            PadReleased = null;
            PadModsPressed = null;

            for (int i = 0; i < 100; i++)
                if (!buttons[i].Empty)
                    buttons[i].PointerPressed -= MouseDown;

            buttons = null;
            grid.Children.Clear();

            //Preferences.LaunchpadModelChanged -= Update_LaunchpadModel;
            //Preferences.LaunchpadStyleChanged -= Update_LaunchpadStyle;
            //Preferences.LaunchpadGridRotationChanged -= Update_LaunchpadRotation;
        }

        public void RenderFrame(Frame frame) {
            for (int i = 0; i < 101; i++)
                SetColor(SignalToGrid(i), frame.Screen[i].ToScreenBrush());
        }

        bool mouseHeld = false;
        IControl mouseOver = null;

        void MouseDown(object sender, PointerPressedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton == PointerUpdateKind.LeftButtonPressed) {
                mouseHeld = true;

                e.Pointer.Capture(root);
                Root.Cursor = new Cursor(StandardCursorType.Hand);

                PadStarted?.Invoke(Array.IndexOf(buttons, (IControl)sender));
                MouseMove(sender, e);
            }
        }

        void MouseUp(object sender, PointerReleasedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton == PointerUpdateKind.LeftButtonReleased) {
                MouseMove(sender, e);
                PadFinished?.Invoke(Array.IndexOf(buttons, (IControl)sender));

                mouseHeld = false;
                if (mouseOver != null) MouseLeave(mouseOver);
                mouseOver = null;

                e.Pointer.Capture(null);
                Root.Cursor = new Cursor(StandardCursorType.Arrow);
            }
        }

        void MouseEnter(IControl control, KeyModifiers mods) {
            int index = Array.IndexOf(buttons, control);
            PadPressed?.Invoke(index);
            PadModsPressed?.Invoke(index, mods);
        }

        void MouseLeave(IControl control) => PadReleased?.Invoke(Array.IndexOf(buttons, control));

        void MouseMove(object sender, PointerEventArgs e) {
            if (mouseHeld) {
                IInputElement _over = Root.InputHitTest(e.GetPosition(Root));

                if (_over is Shape overPath && !(_over is Rectangle))
                    _over = overPath.Parent;

                if (_over is Canvas overCanvas)
                    _over = overCanvas.Parent;
                
                if (_over is LaunchpadButton || _over is Rectangle) {
                    IControl over = (IControl)_over;

                    if (mouseOver == null) MouseEnter(over, e.KeyModifiers);
                    else if (mouseOver != over) {
                        MouseLeave(mouseOver);
                        MouseEnter(over, e.KeyModifiers);
                    }

                    mouseOver = over;

                } else if (mouseOver != null) {
                    MouseLeave(mouseOver);
                    mouseOver = null;
                }
            }
        }
    }
}
