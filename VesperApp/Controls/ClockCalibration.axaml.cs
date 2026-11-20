using System;
using Avalonia.Controls;

namespace VesperApp.Controls;

public partial class ClockCalibration : UserControl
{
    /// <summary>Share of the page height the unit's texts may take before they scroll; the rest is the history.</summary>
    private const double DetailsShare = 0.4;
    private const double DetailsMinHeight = 96;

    public ClockCalibration()
    {
        InitializeComponent();
        PageGrid.SizeChanged += (_, e) => DetailsScroll.MaxHeight = DetailsMaxHeight(e.NewSize.Height);
    }

    internal static double DetailsMaxHeight(double pageHeight) => Math.Max(DetailsMinHeight, pageHeight * DetailsShare);
}
