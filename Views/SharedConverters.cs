using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using KeyPulse.Configuration;
using KeyPulse.Helpers;
using KeyPulse.Models;
using MahApps.Metro.IconPacks;

namespace KeyPulse.Views;

public sealed class PauseStateToIconKindConverter : IValueConverter
{
    // Paused shows the resume affordance (play); tracking shows the pause affordance.
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? PackIconPhosphorIconsKind.Play : PackIconPhosphorIconsKind.Pause;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class PauseStateToActionTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true
            ? "Resume input tracking for the current session"
            : "Pause input tracking for the current session";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class DurationSecondsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long seconds)
            return string.Empty;

        return TimeFormatter.FormatDuration(TimeSpan.FromSeconds(seconds));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ActivityColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool isActive)
            return AppColorPalette.PrimaryTextBrush;

        return isActive ? AppColorPalette.ActiveBrush : AppColorPalette.PrimaryTextBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class DeviceTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DeviceTypes deviceType || parameter is not string typeStr)
            return Visibility.Collapsed;

        var targetTypeEnum = Enum.TryParse<DeviceTypes>(typeStr, out var result) ? result : DeviceTypes.Unknown;
        return deviceType == targetTypeEnum ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class MinWidthToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double width)
            return Visibility.Collapsed;

        if (!double.TryParse(parameter?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var minWidth))
            minWidth = 0;

        return width >= minWidth ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
