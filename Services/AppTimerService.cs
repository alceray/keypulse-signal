using System.Windows.Threading;

namespace KeyPulse.Services;

/// <summary>
/// Singleton service that owns all shared periodic UI-thread timers and broadcasts tick events
/// to any number of subscriber view-models and services. Owning timers here keeps transient
/// view-models lean: they subscribe on construction and unsubscribe on dispose.
/// </summary>
public sealed class AppTimerService : IDisposable
{
    private readonly DispatcherTimer _secondTimer;
    private readonly DispatcherTimer _thirtySecondTimer;
    private readonly DispatcherTimer _minuteTimer;
    private readonly DispatcherTimer _dailyTimer;
    private bool _disposed;

    /// <summary>Raised on the UI thread every second.</summary>
    public event EventHandler? SecondTick;

    /// <summary>Raised on the UI thread every 30 seconds.</summary>
    public event EventHandler? ThirtySecondTick;

    /// <summary>Raised on the UI thread every 60 seconds.</summary>
    public event EventHandler? MinuteTick;

    /// <summary>Raised on the UI thread every 24 hours.</summary>
    public event EventHandler? DailyTick;

    public AppTimerService()
    {
        _secondTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _secondTimer.Tick += (_, _) => SecondTick?.Invoke(this, EventArgs.Empty);
        _secondTimer.Start();

        _thirtySecondTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _thirtySecondTimer.Tick += (_, _) => ThirtySecondTick?.Invoke(this, EventArgs.Empty);
        _thirtySecondTimer.Start();

        _minuteTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _minuteTimer.Tick += (_, _) => MinuteTick?.Invoke(this, EventArgs.Empty);
        _minuteTimer.Start();

        _dailyTimer = new DispatcherTimer { Interval = TimeSpan.FromDays(1) };
        _dailyTimer.Tick += (_, _) => DailyTick?.Invoke(this, EventArgs.Empty);
        _dailyTimer.Start();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _secondTimer.Stop();
        _thirtySecondTimer.Stop();
        _minuteTimer.Stop();
        _dailyTimer.Stop();
    }
}
