using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using KeyPulse.Helpers;
using KeyPulse.Models;

namespace KeyPulse.Views;

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

public sealed class MinutesToDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int minutes)
            return string.Empty;

        return TimeFormatter.FormatDuration(TimeSpan.FromMinutes(minutes));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ActivityColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool isActive)
            return new SolidColorBrush(Colors.Black);

        return isActive ? new SolidColorBrush(Color.FromRgb(0, 102, 204)) : new SolidColorBrush(Colors.Black);
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
