using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MahApps.Metro.IconPacks;

namespace KeyPulse.Helpers;

/// <summary>
/// Rasterizes a Phosphor icon to a GDI+ bitmap for WinForms surfaces such as the tray menu, where the
/// WPF icon control cannot be hosted. The control's template does not apply off the visual tree, so the
/// raw path geometry is drawn directly and fitted to the target square the way the control's Viewbox would.
/// Must be called on the UI thread because it uses WPF rendering.
/// </summary>
public static class PhosphorIconRenderer
{
    public static System.Drawing.Bitmap Render(
        PackIconPhosphorIconsKind kind,
        int sizePx,
        Color color,
        double padding = 1
    )
    {
        var pathData = new PackIconPhosphorIcons { Kind = kind }.Data;
        var geometry = Geometry.Parse(pathData);
        var bounds = geometry.Bounds;

        var available = sizePx - 2 * padding;
        var scale = Math.Min(available / bounds.Width, available / bounds.Height);
        var offsetX = (sizePx - bounds.Width * scale) / 2 - bounds.X * scale;
        var offsetY = (sizePx - bounds.Height * scale) / 2 - bounds.Y * scale;

        var transform = new TransformGroup();
        transform.Children.Add(new ScaleTransform(scale, scale));
        transform.Children.Add(new TranslateTransform(offsetX, offsetY));

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(transform);
            dc.DrawGeometry(new SolidColorBrush(color), null, geometry);
            dc.Pop();
        }

        var target = new RenderTargetBitmap(sizePx, sizePx, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;
        return new System.Drawing.Bitmap(stream);
    }
}
