using System.Windows;
using System.Windows.Input;
using KeyPulse.ViewModels;
using KeyPulse.ViewModels.Calendar;
using Microsoft.Extensions.DependencyInjection;

namespace KeyPulse.Views;

public partial class CalendarView
{
    // Progressive hold acceleration: slow start → medium → fast.
    // Each stage activates after the key has been held for HoldThreshold milliseconds.
    private static readonly (int HoldThresholdMs, int RepeatIntervalMs)[] ArrowRepeatStages =
    [
        (0, 350),
        (1200, 175),
        (2500, 80),
    ];

    private DateTime _arrowHoldStartUtc = DateTime.MinValue;
    private DateTime _lastArrowMoveAtUtc = DateTime.MinValue;
    private Key _currentHeldArrowKey = Key.None;

    private TimeSpan GetCurrentArrowRepeatInterval()
    {
        var holdMs = (DateTime.UtcNow - _arrowHoldStartUtc).TotalMilliseconds;
        var intervalMs = ArrowRepeatStages[0].RepeatIntervalMs;
        foreach (var (threshold, interval) in ArrowRepeatStages)
        {
            if (holdMs >= threshold)
                intervalMs = interval;
        }
        return TimeSpan.FromMilliseconds(intervalMs);
    }

    public CalendarView()
    {
        InitializeComponent();
        DataContext = App.ServiceProvider.GetRequiredService<CalendarViewModel>();
    }

    private void CalendarView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && DataContext is CalendarViewModel vm)
        {
            vm.OnVisible();

            // Ensure the calendar can receive arrow keys immediately after tab switches.
            Dispatcher.BeginInvoke(new Action(() => Keyboard.Focus(this)));
        }
    }

    private void DayTile_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is CalendarDaySummary summary)
        {
            if (DataContext is CalendarViewModel vm)
                vm.SelectDayCommand.Execute(summary);

            // Preserve keyboard navigation after mouse interactions.
            Keyboard.Focus(this);
        }
    }

    private async void CalendarView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not CalendarViewModel vm)
            return;

        var delta = e.Key switch
        {
            Key.Left => -1,
            Key.Right => 1,
            _ => 0,
        };

        if (delta == 0)
            return;

        var nowUtc = DateTime.UtcNow;

        if (!e.IsRepeat)
        {
            // First press: record hold start and move immediately.
            _arrowHoldStartUtc = nowUtc;
            _currentHeldArrowKey = e.Key;
        }
        else
        {
            // Repeated press: throttle using the current acceleration stage.
            if (nowUtc - _lastArrowMoveAtUtc < GetCurrentArrowRepeatInterval())
            {
                e.Handled = true;
                return;
            }
        }

        e.Handled = true;
        _lastArrowMoveAtUtc = nowUtc;
        await vm.MoveSelectionByArrowAsync(delta);

        // Month changes can rebuild visual elements; re-assert focus for continuous key navigation.
        Keyboard.Focus(this);
    }

    private void CalendarView_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == _currentHeldArrowKey)
        {
            _arrowHoldStartUtc = DateTime.MinValue;
            _currentHeldArrowKey = Key.None;
        }
    }
}
