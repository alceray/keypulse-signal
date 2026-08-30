using KeyPulse.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace KeyPulse.Views;

public partial class SettingsView
{
    public SettingsView()
    {
        InitializeComponent();
        var viewModel = App.ServiceProvider.GetRequiredService<SettingsViewModel>();
        DataContext = viewModel;
        // A password box cannot be bound, so the saved password is pushed in by hand.
        PostgreSqlPasswordBox.Password = viewModel.PostgreSqlPassword;
    }

    private void OnPostgreSqlPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel && sender is System.Windows.Controls.PasswordBox passwordBox)
            viewModel.PostgreSqlPassword = passwordBox.Password;
    }

    private void OnCancelDatabaseChangesClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
            return;
        viewModel.CancelDatabaseChanges();
        PostgreSqlPasswordBox.Password = viewModel.PostgreSqlPassword;
    }
}
