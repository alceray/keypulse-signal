using System.Windows;
using KeyPulse.Helpers;

namespace KeyPulse.ViewModels;

public abstract class StatusMessageViewModelBase : ObservableObject, IDisposable
{
    private readonly StatusClearTimer _statusClearTimer;
    private string _statusMessage = string.Empty;

    protected StatusMessageViewModelBase()
    {
        _statusClearTimer = new StatusClearTimer();
        _statusClearTimer.Elapsed += (_, _) => StatusMessage = string.Empty;
    }

    public string StatusMessage
    {
        get => _statusMessage;
        protected set
        {
            if (_statusMessage == value)
                return;

            _statusMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusVisibility));

            if (!string.IsNullOrEmpty(value))
                _statusClearTimer.Restart();
        }
    }

    public Visibility StatusVisibility =>
        string.IsNullOrEmpty(_statusMessage) ? Visibility.Collapsed : Visibility.Visible;

    public virtual void Dispose()
    {
        _statusClearTimer.Dispose();
    }
}
