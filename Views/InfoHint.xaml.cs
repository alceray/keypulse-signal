using System.Windows;

namespace KeyPulse.Views;

/// <summary>
/// A small info icon that shows explanatory text on hover. Use it to attach a one-line hint to a
/// setting without taking up layout space for a caption.
/// </summary>
public partial class InfoHint
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(InfoHint),
        new PropertyMetadata("")
    );

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public InfoHint()
    {
        InitializeComponent();
    }
}
