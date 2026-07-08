using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using VesperApp.ViewModels;

namespace VesperApp.Views
{
    /// <summary>Guided device-import dialog. Returns an <see cref="ImportDeviceRequest"/>
    /// via ShowDialog, or null when cancelled.</summary>
    public partial class ImportDeviceWindow : Window
    {
        public ImportDeviceWindow()
        {
            InitializeComponent();

            btnImport.Click += (_, _) =>
            {
                if (DataContext is ImportDeviceWindowViewModel vm)
                    Close(vm.BuildRequest());
            };
            btnCancel.Click += (_, _) => Close(null);
        }

        protected override void OnClosed(EventArgs e)
        {
            (DataContext as ImportDeviceWindowViewModel)?.TerminateScan();
            base.OnClosed(e);
        }
    }
}
