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
    private readonly DispatcherTimer _hourlyTimer;
    private bool _disposed;

    /// <summary>Raised on the UI thread every second.</summary>
    public event EventHandler? SecondTick;

    /// <summary>Raised on the UI thread every 30 seconds.</summary>
    public event EventHandler? ThirtySecondTick;

    /// <summary>Raised on the UI thread every hour.</summary>
    public event EventHandler? HourlyTick;

    public AppTimerService()
    {
        _secondTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _secondTimer.Tick += (_, _) => SecondTick?.Invoke(this, EventArgs.Empty);
        _secondTimer.Start();

        _thirtySecondTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _thirtySecondTimer.Tick += (_, _) => ThirtySecondTick?.Invoke(this, EventArgs.Empty);
        _thirtySecondTimer.Start();

        _hourlyTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(1) };
        _hourlyTimer.Tick += (_, _) => HourlyTick?.Invoke(this, EventArgs.Empty);
        _hourlyTimer.Start();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _secondTimer.Stop();
        _thirtySecondTimer.Stop();
        _hourlyTimer.Stop();
    }
}
