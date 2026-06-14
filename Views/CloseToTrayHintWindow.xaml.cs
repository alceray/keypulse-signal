using System.Windows;

namespace KeyPulse.Views;

/// <summary>
/// The reminder shown when the window closes into the tray, explaining the app keeps running.
/// Read DontShowAgain once the dialog closes to learn whether the user opted out.
/// </summary>
public partial class CloseToTrayHintWindow : Window
{
    public CloseToTrayHintWindow(string caption)
    {
        InitializeComponent();
        Title = caption;
    }

    public bool DontShowAgain => DontShowAgainCheckBox.IsChecked == true;

    private void OnOkClick(object sender, RoutedEventArgs e) => Close();
}
