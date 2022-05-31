using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Diagnostics;

namespace VesperApp.Controls
{
    public partial class DeviceDriverPropertyGrid : UserControl
    {
        private DataGrid gridEditor;
        public DeviceDriverPropertyGrid()
        {
            InitializeComponent();

            gridEditor = this.FindControl<DataGrid>("gridEditProperties");
            gridEditor.DataContextChanged += GridEditor_DataContextChanged;
        }

        private void GridEditor_DataContextChanged(object? sender, System.EventArgs e)
        {
            Debug.WriteLine("Grid DataContext chnaged");
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
