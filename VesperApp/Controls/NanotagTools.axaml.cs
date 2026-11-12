using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using VesperApp.ViewModels;

namespace VesperApp.Controls;

public partial class NanotagTools : UserControl
{
    private ScrollViewer? _consoleScroll;
    private bool _followConsole = true;     // at the end when new output arrives: keep following it
    private NanotagToolsViewModel? _vm;

    public NanotagTools()
    {
        InitializeComponent();

        /* The console follows new output only while it is scrolled to the end: scrolled up to read an earlier
           reply, it stays there (it used to jump back to the end on every line). Scrolling back to the end, or
           Clear, resumes following. */
        ConsoleBox.TemplateApplied += (_, e) =>
        {
            _consoleScroll = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
            if (_consoleScroll != null)
                _consoleScroll.ScrollChanged += (_, _) => _followConsole = IsAtEnd(_consoleScroll);
        };
        ConsoleBox.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextBox.TextProperty) return;
            if (string.IsNullOrEmpty(ConsoleBox.Text)) _followConsole = true;
            if (!_followConsole) return;
            ConsoleBox.CaretIndex = ConsoleBox.Text?.Length ?? 0;
            Dispatcher.UIThread.Post(() => _consoleScroll?.ScrollToEnd(), DispatcherPriority.Background);
        };

        // A fresh read of the configuration starts at its top.
        DataContextChanged += (_, _) =>
        {
            if (_vm != null) _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = DataContext as NanotagToolsViewModel;
            if (_vm != null) _vm.PropertyChanged += OnVmPropertyChanged;
        };
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NanotagToolsViewModel.ConfigText))
            Dispatcher.UIThread.Post(() => ConfigScroll.Offset = new Vector(0, 0));
    }

    private static bool IsAtEnd(ScrollViewer sv) =>
        sv.Offset.Y >= Math.Max(0, sv.Extent.Height - sv.Viewport.Height) - 4;

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not NanotagToolsViewModel vm) return;
        if (e.Key == Key.Up) { vm.HistoryUp(); InputBox.CaretIndex = InputBox.Text?.Length ?? 0; e.Handled = true; }
        else if (e.Key == Key.Down) { vm.HistoryDown(); InputBox.CaretIndex = InputBox.Text?.Length ?? 0; e.Handled = true; }
    }
}
