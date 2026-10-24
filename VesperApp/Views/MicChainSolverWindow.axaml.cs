using Avalonia.Controls;

namespace VesperApp.Views
{
    /// <summary>Microphone Solve window (modal, over the configuration editor).</summary>
    public partial class MicChainSolverWindow : Window
    {
        public MicChainSolverWindow()
        {
            InitializeComponent();
            btnClose.Click += (_, _) => Close();
        }
    }
}
