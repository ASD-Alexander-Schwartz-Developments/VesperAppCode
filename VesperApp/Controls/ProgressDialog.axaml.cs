using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using Octokit;
using VesperApp.ViewModels;

namespace VesperApp.Controls;

public partial class ProgressDialog : UserControl
{
    public ProgressDialog()
    {
        InitializeComponent();
    }
}