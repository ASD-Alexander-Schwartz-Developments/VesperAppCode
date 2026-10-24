using Avalonia.Controls;
using VesperApp.ViewModels;

namespace VesperApp.Views
{
    /// <summary>Recording helper (modal, over the configuration editor). Apply writes the choices to the driver.</summary>
    public partial class MicHelperWindow : Window
    {
        public MicHelperWindow()
        {
            InitializeComponent();
            btnCancel.Click += (_, _) => Close();
            btnApply.Click += (_, _) =>
            {
                (DataContext as MicHelperViewModel)?.Apply();
                Close();
            };
        }
    }
}
