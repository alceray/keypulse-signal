using KeyPulse.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace KeyPulse.Views;

public partial class SettingsView
{
    public SettingsView()
    {
        InitializeComponent();
        DataContext = App.ServiceProvider.GetRequiredService<SettingsViewModel>();
    }

    private void OnPostgreSqlPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel && sender is System.Windows.Controls.PasswordBox passwordBox)
            viewModel.PostgreSqlPassword = passwordBox.Password;
    }

    private void OnEditDatabaseConnectionClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
            return;
        viewModel.BeginEditConnection();
        // A password box cannot be bound, so clearing it is done by hand.
        PostgreSqlPasswordBox.Clear();
    }

    private void OnCancelDatabaseChangesClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
            return;
        viewModel.CancelDatabaseChanges();
        PostgreSqlPasswordBox.Clear();
    }
}
