using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace VesperApp.Controls {
    public partial class VerticalAdd: AddButton {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
            
            root = this.Get<Grid>("Root");
            path = this.Get<Path>("Path");
            icon = this.Get<Canvas>("Icon");
        }
        
        public delegate void ActionEventHandler(string action);
        public event ActionEventHandler Action;

        Canvas icon;

        public enum AvailableActions {
            None, Paste, PasteAndImport
        }

        AvailableActions _actions = AvailableActions.None;
        public AvailableActions Actions {
            get => _actions;
            set {
                if (value != _actions) {
                    _actions = value;

                    if (_actions == AvailableActions.None) ActionContextMenu = null;
                    else if (_actions == AvailableActions.Paste) ActionContextMenu = (VesperContextMenu)this.Resources["PasteContextMenu"];
                    else if (_actions == AvailableActions.PasteAndImport) ActionContextMenu = (VesperContextMenu)this.Resources["PasteAndImportContextMenu"];
                }
            }
        }

        ContextMenu ActionContextMenu = null;

        public override bool AlwaysShowing {
            set {
                if (value != _always) {
                    _always = value;
                    Root.MinHeight = _always? 26 : 0;
                }
            }
        }
        
        public VerticalAdd() {
            InitializeComponent();

            AllowRightClick = true;

            base.MouseLeave(this, null);
        }

        protected override void Unloaded(object sender, VisualTreeAttachmentEventArgs e) {
            ActionContextMenu = null;
            
            Action = null;
            base.Unloaded(sender, e);
        }

        void ContextMenu_Action(string action) => Action?.Invoke(action);

        protected override void Click(PointerReleasedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton == PointerUpdateKind.LeftButtonReleased) InvokeAdded();
            else if (MouseButton == PointerUpdateKind.RightButtonReleased) ActionContextMenu?.Open(Icon);
        }
    }
}
