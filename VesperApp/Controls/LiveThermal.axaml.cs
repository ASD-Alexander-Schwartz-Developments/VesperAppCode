using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using VesperApp.ViewModels;

namespace VesperApp.Controls;

public partial class LiveThermal : UserControl
{
    public LiveThermal()
    {
        InitializeComponent();
    }

    /// <summary>Pointer position -> image pixel (the image is drawn Stretch=Uniform, centred).</summary>
    private void OnImagePointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not LiveThermalViewModel vm || sender is not Image img || img.Source is not Avalonia.Media.Imaging.Bitmap bmp)
            return;
        Size box = img.Bounds.Size;
        double pw = bmp.PixelSize.Width, ph = bmp.PixelSize.Height;
        double scale = System.Math.Min(box.Width / pw, box.Height / ph);
        if (scale <= 0) return;
        double ox = (box.Width - pw * scale) / 2, oy = (box.Height - ph * scale) / 2;
        Point p = e.GetPosition(img);
        vm.SetHover((int)System.Math.Floor((p.X - ox) / scale), (int)System.Math.Floor((p.Y - oy) / scale));
    }

    private void OnImagePointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is LiveThermalViewModel vm) vm.SetHover(null, null);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // leaving the page (or closing the app) ends the stream and powers the camera down
        if (DataContext is LiveThermalViewModel vm && vm.IsStreaming) _ = vm.StopAsync();
    }
}
