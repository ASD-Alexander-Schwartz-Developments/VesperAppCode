using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using VesperApp.Controls;

namespace VesperApp.Views;

public partial class ProgressDialogWindow : Window
{
    private readonly IProgressStatus _progressStatus;

    public ProgressDialogWindow(string progressWindowTitle, IProgressStatus ps, Window? owner = null)
    {
        _progressStatus = ps ?? throw new ArgumentNullException(nameof(ps));
        DataContext = ps;
        Title = progressWindowTitle;
        InitializeComponent();
        Closing += ProgressDialogWindow_Closing;
        Owner = owner;
    }

    private void ProgressDialogWindow_Closing(object? _, System.ComponentModel.CancelEventArgs __)
    {
        _progressStatus.CancelCommand.Execute(null);
    }
}