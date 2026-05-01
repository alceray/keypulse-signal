using System.Windows.Threading;
using KeyPulse.Configuration;

namespace KeyPulse.Helpers;

/// <summary>
/// A one-shot countdown timer that fires <see cref="Elapsed"/> after a configurable delay.
/// Calling <see cref="Restart"/> resets the countdown so rapid calls only trigger one clear.
/// Intended for auto-dismissing transient status messages in view-models.
/// </summary>
public sealed class StatusClearTimer : IDisposable
{
    public const int DefaultIntervalSeconds = 3;

    private readonly DispatcherTimer _timer;
    private bool _disposed;

    /// <summary>Raised on the UI thread once the countdown completes.</summary>
    public event EventHandler? Elapsed;

    public StatusClearTimer(int seconds = DefaultIntervalSeconds)
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        _timer.Tick += OnTick;
    }

    /// <summary>Cancels any running countdown and starts a fresh one.</summary>
    public void Restart()
    {
        _timer.Stop();
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        Elapsed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
