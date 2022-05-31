using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;

namespace VesperApp.Controls {
    public abstract class AddButton: UserControl {
        public delegate void AddedEventHandler();
        public event AddedEventHandler Added;

        protected void InvokeAdded() => Added?.Invoke();

        protected Path path;

        protected IBrush Fill {
            get => path.Stroke;
            set => path.Stroke = value;
        }

        protected bool AllowRightClick = false;

        protected Grid root;

        protected bool _always;
        public virtual bool AlwaysShowing {
            get => _always;
            set {}
        }

        protected virtual void Unloaded(object sender, VisualTreeAttachmentEventArgs e) => Added = null;

        bool mouseHeld = false;

        protected void MouseEnter(object sender, PointerEventArgs e) {
            Fill = (IBrush)Application.Current.FindResource(mouseHeld? "ThemeButtonDownBrush" : "ThemeButtonOverBrush");
        }

        protected void MouseLeave(object sender, PointerEventArgs e) {
            Fill = (IBrush)Application.Current.FindResource("ThemeButtonEnabledBrush");
            mouseHeld = false;
        }

        protected void MouseDown(object sender, PointerPressedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton == PointerUpdateKind.LeftButtonPressed || (AllowRightClick && MouseButton == PointerUpdateKind.RightButtonPressed)) {
                mouseHeld = true;

                Fill = (IBrush)Application.Current.FindResource("ThemeButtonDownBrush");
            }
        }

        protected void MouseUp(object sender, PointerReleasedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (mouseHeld && (MouseButton == PointerUpdateKind.LeftButtonReleased || (AllowRightClick && MouseButton == PointerUpdateKind.RightButtonReleased))) {
                mouseHeld = false;

                MouseEnter(sender, null);

                Click(e);
            }
        }

        protected virtual void Click(PointerReleasedEventArgs e) => Added?.Invoke();
    }
}
