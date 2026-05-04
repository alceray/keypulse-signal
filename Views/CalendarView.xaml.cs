using System.Globalization;
using System.Windows;
using System.Windows.Data;
using KeyPulse.Helpers;
using KeyPulse.ViewModels;
using KeyPulse.ViewModels.Calendar;
using Microsoft.Extensions.DependencyInjection;

namespace KeyPulse.Views;

public partial class CalendarView
{
    public CalendarView()
    {
        InitializeComponent();
        DataContext = App.ServiceProvider.GetRequiredService<CalendarViewModel>();
    }

    private void CalendarView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && DataContext is CalendarViewModel vm)
            vm.OnVisible();
    }

    private void DayTile_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is CalendarDaySummary summary)
        {
            if (DataContext is CalendarViewModel vm)
                vm.SelectDayCommand.Execute(summary);
        }
    }
}

/// <summary>true → Visible, false → Collapsed</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>true → Collapsed, false → Visible</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Formats a long (seconds) as h:mm or d h:mm for tile display.</summary>
public sealed class SecondsToHhMmConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long seconds)
            return "";
        var ts = TimeSpan.FromSeconds(seconds);
        var totalHours = (long)ts.TotalHours;
        return $"{totalHours}:{ts.Minutes:D2}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Formats a long (seconds) as h:mm:ss for detail panel display.</summary>
public sealed class SecondsToHhMmSsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long seconds)
            return "";
        return TimeFormatter.FormatDuration(TimeSpan.FromSeconds(seconds));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Formats a large number compactly: 1000 → 1k, 1000000 → 1M.</summary>
public sealed class LargeNumberConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long n)
            return "";
        if (n >= 1_000_000)
            return $"{n / 1_000_000.0:F1}M";
        if (n >= 1_000)
            return $"{n / 1_000.0:F1}k";
        return n.ToString();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
