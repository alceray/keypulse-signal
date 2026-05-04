using System.Windows;

namespace KeyPulse.Views;

public partial class StatusMessagePanel 
{
    public static readonly DependencyProperty StatusMessageProperty = DependencyProperty.Register(
        nameof(StatusMessage),
        typeof(string),
        typeof(StatusMessagePanel),
        new PropertyMetadata("")
    );

    public static readonly DependencyProperty StatusVisibilityProperty = DependencyProperty.Register(
        nameof(StatusVisibility),
        typeof(Visibility),
        typeof(StatusMessagePanel),
        new PropertyMetadata(Visibility.Collapsed)
    );

    public string StatusMessage
    {
        get => (string)GetValue(StatusMessageProperty);
        set => SetValue(StatusMessageProperty, value);
    }

    public Visibility StatusVisibility
    {
        get => (Visibility)GetValue(StatusVisibilityProperty);
        set => SetValue(StatusVisibilityProperty, value);
    }

    public StatusMessagePanel()
    {
        InitializeComponent();
    }
}
