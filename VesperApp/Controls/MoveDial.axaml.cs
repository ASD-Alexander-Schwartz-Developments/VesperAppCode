using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using VesperApp.Models;

namespace VesperApp.Controls {
    public partial class MoveDial: UserControl {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            planeCanvas = this.Get<Canvas>("PlaneCanvas");
            absoluteCanvas = this.Get<Canvas>("AbsoluteCanvas");

            xRect = this.Get<Rectangle>("XRect");
            yRect = this.Get<Rectangle>("YRect");
            point = this.Get<Rectangle>("Point");
            absolutePoint = this.Get<Rectangle>("AbsolutePoint");

            display = this.Get<TextBlock>("Display");

            inputX = this.Get<TextBox>("InputX");
            inputY = this.Get<TextBox>("InputY");
        }

        HashSet<IDisposable> observables = new();

        public delegate void ChangedEventHandler(int x, int y, int? old_x, int? old_y);
        public event ChangedEventHandler Changed, AbsoluteChanged;
        
        public delegate void SwitchedEventHandler();
        public event SwitchedEventHandler Switched;

        Canvas planeCanvas, absoluteCanvas;
        Rectangle xRect, yRect, point, absolutePoint;
        TextBlock display;
        TextBox inputX, inputY;

        int _x = 0;
        public int X {
            get => _x;
            set {
                value = Math.Max(-9, Math.Min(9, value));
                if (value != _x) {
                    _x = value;
                    DrawX();
                    DrawPoint();
                    Changed?.Invoke(_x, _y, null, null);
                }
            }
        }

        int _y = 0;
        public int Y {
            get => _y;
            set {
                value = Math.Max(-9, Math.Min(9, value));
                if (value != _y) {
                    _y = value;
                    DrawY();
                    DrawPoint();
                    Changed?.Invoke(_x, _y, null, null);
                }
            }
        }

        int _ax = 0;
        public int AbsoluteX {
            get => _ax;
            set {
                value = Math.Max(0, Math.Min(9, value));
                if (value != _ax) {
                    _ax = value;
                    DrawPoint();
                    AbsoluteChanged?.Invoke(_ax, _ay, null, null);
                }
            }
        }

        int _ay = 0;
        public int AbsoluteY {
            get => _ay;
            set {
                value = Math.Max(0, Math.Min(9, value));
                if (value != _ay) {
                    _ay = value;
                    DrawPoint();
                    AbsoluteChanged?.Invoke(_ax, _ay, null, null);
                }
            }
        }

        Canvas CurrentCanvas => absoluteCanvas.IsVisible? absoluteCanvas : planeCanvas;
        int CurrentX => absoluteCanvas.IsVisible? AbsoluteX : X;
        int CurrentY => absoluteCanvas.IsVisible? AbsoluteY : Y;

        string ValueString => $"({(absoluteCanvas.IsVisible? _ax : _x)}, {(absoluteCanvas.IsVisible? _ay : _y)})";

        void DrawPoint() {
            Canvas.SetLeft(Point, 18 + 2 * _x);
            Canvas.SetTop(Point, 18 - 2 * _y);

            Canvas.SetLeft(absolutePoint, 4 * _ax);
            Canvas.SetTop(absolutePoint, 36 - 4 * _ay);

            display.Text = ValueString;
        }

        void DrawX() {
            xRect.Width = Math.Abs(2 * _x) + 2;
            Canvas.SetLeft(xRect, (_x > 0)? 18 : 20 - xRect.Width);
        }

        void DrawY() {
            yRect.Height = Math.Abs(2 * _y) + 2;
            Canvas.SetTop(yRect, (_y < 0)? 18 : 20 - yRect.Height);
        }

        public MoveDial() {
            InitializeComponent();

            observables.Add(InputX.GetObservable(TextBox.TextProperty).Subscribe(InputX_Changed));
            inputX.AddHandler(InputElement.PointerPressedEvent, Input_MouseDown, RoutingStrategies.Tunnel);

            observables.Add(InputY.GetObservable(TextBox.TextProperty).Subscribe(InputY_Changed));
            inputY.AddHandler(InputElement.PointerPressedEvent, Input_MouseDown, RoutingStrategies.Tunnel);

            DrawX();
            DrawY();
            DrawPoint();
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) {
            Changed = null;
            AbsoluteChanged = null;
            Switched = null;

            inputX.RemoveHandler(InputElement.PointerPressedEvent, Input_MouseDown);
            inputY.RemoveHandler(InputElement.PointerPressedEvent, Input_MouseDown);

            foreach (IDisposable observable in observables)
                observable.Dispose();
        }

        public void Update(Offset offset) {
            X = offset.X;
            Y = offset.Y;

            AbsoluteX = offset.AbsoluteX;
            AbsoluteY = offset.AbsoluteY;

            planeCanvas.IsVisible = !(absoluteCanvas.IsVisible = offset.IsAbsolute);
            DrawPoint();
        }

        bool mouseHeld = false;
        int old_x, old_y;
        double lastX, lastY;

        void MouseDown(object sender, PointerPressedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton == PointerUpdateKind.LeftButtonPressed) {
                if (e.ClickCount == 2) {
                    DisplayPressed(sender, e);
                    return;
                }

                mouseHeld = true;
                e.Pointer.Capture(CurrentCanvas);

                lastX = e.GetPosition(CurrentCanvas).X;
                lastY = e.GetPosition(CurrentCanvas).Y;
                old_x = CurrentX;
                old_y = CurrentY;

                CurrentCanvas.Cursor = new Cursor(StandardCursorType.SizeAll);
            }
        }

        void MouseUp(object sender, PointerReleasedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
            
            if (MouseButton == PointerUpdateKind.LeftButtonReleased) {
                mouseHeld = false;
                e.Pointer.Capture(null);

                if (absoluteCanvas.IsVisible) {
                    if (old_x != AbsoluteX || old_y != AbsoluteY)
                        AbsoluteChanged?.Invoke(AbsoluteX, AbsoluteY, old_x, old_y);

                } else {
                    if (old_x != X || old_y != Y)
                        Changed?.Invoke(X, Y, old_x, old_y);
                }

                CurrentCanvas.Cursor = new Cursor(StandardCursorType.Hand);

            } else if (!mouseHeld && MouseButton == PointerUpdateKind.RightButtonReleased)
                Switched?.Invoke();
        }

        void MouseMove(object sender, PointerEventArgs e) {
            if (mouseHeld) {
                double x = e.GetPosition(CurrentCanvas).X;
                double y = e.GetPosition(CurrentCanvas).Y;

                if (Math.Abs(x - lastX) >= 4) {
                    int change = (int)((x - lastX) / 4);

                    if (absoluteCanvas.IsVisible) AbsoluteX += change;
                    else X += change;
                    
                    lastX = x;
                }

                if (Math.Abs(y - lastY) >= 4) {
                    int change = -(int)((y - lastY) / 4);

                    if (absoluteCanvas.IsVisible) AbsoluteY += change;
                    else Y += change;

                    lastY = y;
                }
            }
        }

        Action InputX_Update = null, InputY_Update = null;

        void InputX_Changed(string text) {
            int x = Input_Changed(inputX, InputX_Update, CurrentX, text);

            if (absoluteCanvas.IsVisible) AbsoluteX = x;
            else X = x;
        }

        void InputY_Changed(string text) {
            int y = Input_Changed(inputY, InputY_Update, CurrentY, text);
            
            if (absoluteCanvas.IsVisible) AbsoluteY = y;
            else Y = y;
        }

        int Input_Changed(TextBox Input, Action Update, int RawValue, string text) {
            if (text == null) return RawValue;
            if (text == "") return RawValue;

            Update = () => { Input.Text = RawValue.ToString(); };

            if (int.TryParse(text, out int value)) {
                if ((absoluteCanvas.IsVisible? 0 : -9) <= value && value <= 9) {
                    RawValue = value;
                    Update = () => { Input.Foreground = (IBrush)Application.Current.FindResource("ThemeForegroundBrush"); };
                } else {
                    Update = () => { Input.Foreground = (IBrush)Application.Current.FindResource("ErrorBrush"); };
                }

                Update += () => {
                    if (value < 0) text = $"-{text.Substring(1).TrimStart('0')}";
                    else if (value > 0) text = text.TrimStart('0');
                    else text = "0";

                    if (!absoluteCanvas.IsVisible && value < -9) text = "-9";
                    if (value > 9) text = "9";
                    
                    Input.Text = text;
                };
            }

            if (!absoluteCanvas.IsVisible && text == "-") Update = null;

            Dispatcher.UIThread.InvokeAsync(() => {
                Update?.Invoke();
                Update = null;
            });

            return RawValue;
        }

        void DisplayPressed(object sender, PointerPressedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton == PointerUpdateKind.LeftButtonPressed && e.ClickCount == 2) {
                inputX.Text = CurrentX.ToString();
                inputY.Text = CurrentY.ToString();

                inputX.SelectionStart = 0;
                inputX.SelectionEnd = InputX.Text.Length;
                inputX.CaretIndex = InputX.Text.Length;

                display.Opacity = 0;
                display.IsHitTestVisible = false;

                inputX.Opacity = inputY.Opacity = 1;
                inputX.IsHitTestVisible = inputY.IsHitTestVisible = true;
                inputX.Focus();

                e.Handled = true;
            }
        }

        bool SkipLostFocus = false;
        
        void Input_LostFocus(object sender, RoutedEventArgs e) {
            if (SkipLostFocus) {
                SkipLostFocus = false;
                return;
            }

            inputX.Text = CurrentX.ToString();
            inputY.Text = CurrentY.ToString();

            display.Opacity = 1;
            display.IsHitTestVisible = true;

            inputX.Opacity = InputY.Opacity = 0;
            inputX.IsHitTestVisible = InputY.IsHitTestVisible = false;

            if (absoluteCanvas.IsVisible) {
                if (old_x != AbsoluteX || old_y != AbsoluteY)
                    AbsoluteChanged?.Invoke(AbsoluteX, AbsoluteY, old_x, old_y);

            } else {
                if (old_x != X || old_y != Y)
                    Changed?.Invoke(X, Y, old_x, old_y);
            }
        }

        void Input_KeyDown(object sender, KeyEventArgs e) {
            //if (App.Dragging) return;

            if (e.Key == Key.Return)
                this.Focus();
            
            if (e.Key == Key.Tab && sender is TextBox textBox) {
                SkipLostFocus = true;

                TextBox i = (textBox == inputX) ? inputY : inputX;
                
                i.SelectionStart = 0;
                i.SelectionEnd = i.Text.Length;
                i.CaretIndex = i.Text.Length;

                i.Focus();
            }

            e.Key = Key.None;
        }

        void Input_KeyUp(object sender, KeyEventArgs e) {
            //if (App.Dragging) return;

            e.Key = Key.None;
        }

        void Input_MouseDown(object sender, PointerPressedEventArgs e) {
            TextBox Input = (TextBox)sender;

            if (!Input.IsFocused) {
                SkipLostFocus = true;
                Input.Focus();
            }
        }

        void Input_MouseUp(object sender, PointerReleasedEventArgs e) => e.Handled = true;
    }
}
