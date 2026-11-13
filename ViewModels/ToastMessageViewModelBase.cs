using System.Windows;
using KeyPulse.Helpers;

namespace KeyPulse.ViewModels;

public abstract class ToastMessageViewModelBase : ObservableObject, IDisposable
{
    private readonly ToastClearTimer _toastClearTimer;
    private string _toastMessage = string.Empty;

    protected ToastMessageViewModelBase(int toastDurationSeconds = ToastClearTimer.DefaultIntervalSeconds)
    {
        _toastClearTimer = new ToastClearTimer(toastDurationSeconds);
        _toastClearTimer.Elapsed += (_, _) => ToastMessage = string.Empty;
    }

    public string ToastMessage
    {
        get => _toastMessage;
        protected set
        {
            // Repeated actions deserve a full reading interval even when their message is unchanged.
            if (!string.IsNullOrEmpty(value))
                _toastClearTimer.Restart();

            if (_toastMessage == value)
                return;

            _toastMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ToastVisibility));
        }
    }

    public Visibility ToastVisibility =>
        string.IsNullOrEmpty(_toastMessage) ? Visibility.Collapsed : Visibility.Visible;

    public virtual void Dispose()
    {
        _toastClearTimer.Dispose();
    }
}
