using System.Globalization;
using System.Windows;
using System.Windows.Media;
using KeyPulse.Configuration;
using KeyPulse.Models;
using KeyPulse.Views;

namespace KeyPulse.Tests.Views;

/// <summary>
/// All converters are pure value transforms (no Dispatcher needed). Each ConvertBack throws.
/// Notable edge: DurationSecondsConverter accepts only long — passing the wrong type silently yields "".
/// </summary>
public class SharedConvertersTests
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    [Fact]
    public void InverseBoolToVisibility()
    {
        var c = new InverseBoolToVisibilityConverter();
        c.Convert(true, typeof(Visibility), null, Inv).ShouldBe(Visibility.Collapsed);
        c.Convert(false, typeof(Visibility), null, Inv).ShouldBe(Visibility.Visible);
        c.Convert(null, typeof(Visibility), null, Inv).ShouldBe(Visibility.Visible); // non-true => Visible
        Should.Throw<NotSupportedException>(() => c.ConvertBack(null, typeof(bool), null, Inv));
    }

    [Fact]
    public void DurationSeconds()
    {
        var c = new DurationSecondsConverter();
        c.Convert(90L, typeof(string), null, Inv).ShouldBe("1m 30s");
        c.Convert(86400L * 2 + 3600 * 3 + 60 * 4 + 5, typeof(string), null, Inv).ShouldBe("2d 3h 4m 5s"); // full, not capped at 3 units
        c.Convert(0L, typeof(string), null, Inv).ShouldBe("0s");
        c.Convert(-5L, typeof(string), null, Inv).ShouldBe("0s");
        c.Convert(5, typeof(string), null, Inv).ShouldBe(""); // int, not long => empty
        c.Convert(null, typeof(string), null, Inv).ShouldBe("");
        Should.Throw<NotSupportedException>(() => c.ConvertBack(null, typeof(long), null, Inv));
    }

    [Fact]
    public void ActivityColor()
    {
        var c = new ActivityColorConverter();
        c.Convert(true, typeof(Brush), null, Inv).ShouldBe(AppColorPalette.ActiveBrush);
        c.Convert(false, typeof(Brush), null, Inv).ShouldBe(AppColorPalette.PrimaryTextBrush);
        c.Convert(null, typeof(Brush), null, Inv).ShouldBe(AppColorPalette.PrimaryTextBrush); // non-bool => black
        Should.Throw<NotSupportedException>(() => c.ConvertBack(null, typeof(bool), null, Inv));
    }

    [Fact]
    public void DeviceTypeToVisibility()
    {
        var c = new DeviceTypeToVisibilityConverter();
        c.Convert(DeviceTypes.Keyboard, typeof(Visibility), "Keyboard", Inv).ShouldBe(Visibility.Visible);
        c.Convert(DeviceTypes.Mouse, typeof(Visibility), "Keyboard", Inv).ShouldBe(Visibility.Collapsed);
        c.Convert(DeviceTypes.Keyboard, typeof(Visibility), null, Inv).ShouldBe(Visibility.Collapsed); // param not string
        c.Convert("nope", typeof(Visibility), "Keyboard", Inv).ShouldBe(Visibility.Collapsed); // value not enum
        // Unparseable parameter falls back to Unknown, so an Unknown device matches => Visible.
        c.Convert(DeviceTypes.Unknown, typeof(Visibility), "garbage", Inv).ShouldBe(Visibility.Visible);
        Should.Throw<NotSupportedException>(() => c.ConvertBack(null, typeof(DeviceTypes), null, Inv));
    }

    [Fact]
    public void MinWidthToVisibility()
    {
        var c = new MinWidthToVisibilityConverter();
        c.Convert(100.0, typeof(Visibility), "50", Inv).ShouldBe(Visibility.Visible);
        c.Convert(30.0, typeof(Visibility), "50", Inv).ShouldBe(Visibility.Collapsed);
        c.Convert(50.0, typeof(Visibility), "50", Inv).ShouldBe(Visibility.Visible); // >= boundary
        c.Convert(0.0, typeof(Visibility), null, Inv).ShouldBe(Visibility.Visible); // null param => minWidth 0
        c.Convert(5, typeof(Visibility), "1", Inv).ShouldBe(Visibility.Collapsed); // int, not double => Collapsed
        Should.Throw<NotSupportedException>(() => c.ConvertBack(null, typeof(double), null, Inv));
    }
}
