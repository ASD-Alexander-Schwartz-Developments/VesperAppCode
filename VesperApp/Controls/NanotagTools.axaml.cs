using Avalonia.Controls;
using Avalonia.Input;
using VesperApp.ViewModels;

namespace VesperApp.Controls;

public partial class NanotagTools : UserControl
{
    public NanotagTools()
    {
        InitializeComponent();
        ConsoleBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
                ConsoleBox.CaretIndex = ConsoleBox.Text?.Length ?? 0;     // follow the output
        };
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not NanotagToolsViewModel vm) return;
        if (e.Key == Key.Up) { vm.HistoryUp(); InputBox.CaretIndex = InputBox.Text?.Length ?? 0; e.Handled = true; }
        else if (e.Key == Key.Down) { vm.HistoryDown(); InputBox.CaretIndex = InputBox.Text?.Length ?? 0; e.Handled = true; }
    }
}
