using System.Windows;

namespace KeyPulse.Views;

public partial class ToastMessagePanel
{
    public static readonly DependencyProperty ToastMessageProperty = DependencyProperty.Register(
        nameof(ToastMessage),
        typeof(string),
        typeof(ToastMessagePanel),
        new PropertyMetadata("")
    );

    public static readonly DependencyProperty ToastVisibilityProperty = DependencyProperty.Register(
        nameof(ToastVisibility),
        typeof(Visibility),
        typeof(ToastMessagePanel),
        new PropertyMetadata(Visibility.Collapsed)
    );

    public string ToastMessage
    {
        get => (string)GetValue(ToastMessageProperty);
        set => SetValue(ToastMessageProperty, value);
    }

    public Visibility ToastVisibility
    {
        get => (Visibility)GetValue(ToastVisibilityProperty);
        set => SetValue(ToastVisibilityProperty, value);
    }

    public ToastMessagePanel()
    {
        InitializeComponent();
    }
}
