using System.Windows;
using KeyPulse.Helpers;

namespace KeyPulse.ViewModels;

public abstract class ToastMessageViewModelBase : ObservableObject, IDisposable
{
    private readonly ToastClearTimer _toastClearTimer;
    private string _toastMessage = string.Empty;

    protected ToastMessageViewModelBase()
    {
        _toastClearTimer = new ToastClearTimer();
        _toastClearTimer.Elapsed += (_, _) => ToastMessage = string.Empty;
    }

    public string ToastMessage
    {
        get => _toastMessage;
        protected set
        {
            if (_toastMessage == value)
                return;

            _toastMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ToastVisibility));

            if (!string.IsNullOrEmpty(value))
                _toastClearTimer.Restart();
        }
    }

    public Visibility ToastVisibility =>
        string.IsNullOrEmpty(_toastMessage) ? Visibility.Collapsed : Visibility.Visible;

    public virtual void Dispose()
    {
        _toastClearTimer.Dispose();
    }
}
