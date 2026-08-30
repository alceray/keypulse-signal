using System.Windows;
using KeyPulse.Models;
using KeyPulse.Services;
using Serilog;

namespace KeyPulse.Views;

public partial class DatabaseSetupWindow : Window
{
    private readonly AppSettingsService _settingsService;
    private readonly IDatabaseCredentialStore _credentialStore;
    private readonly bool _recoveryMode;

    public DatabaseSetupWindow(
        string caption,
        AppSettingsService settingsService,
        IDatabaseCredentialStore credentialStore,
        bool recoveryMode = false,
        string? failureMessage = null
    )
    {
        InitializeComponent();
        Title = caption;
        _settingsService = settingsService;
        _credentialStore = credentialStore;
        _recoveryMode = recoveryMode;
        SslModeComboBox.ItemsSource = Enum.GetValues<PostgreSqlSslMode>();

        var settings = settingsService.GetSettings();
        var postgreSql = settings.PostgreSql;
        HostTextBox.Text = postgreSql.Host;
        PortTextBox.Text = postgreSql.Port.ToString();
        DatabaseTextBox.Text = postgreSql.Database;
        UsernameTextBox.Text = postgreSql.Username;
        SslModeComboBox.SelectedItem = postgreSql.SslMode;
        PasswordInput.Password = credentialStore.ReadPostgreSqlPassword() ?? string.Empty;

        if (settings.DatabaseProvider == DatabaseProvider.PostgreSql || recoveryMode)
            PostgreSqlRadio.IsChecked = true;

        if (recoveryMode)
        {
            HeadingText.Text = "Database connection required";
            DescriptionText.Text =
                "KeyPulse cannot reach its PostgreSQL database. Update the connection and retry, or explicitly switch to the local SQLite backup.";
        }

        if (!string.IsNullOrWhiteSpace(failureMessage))
            StatusText.Text = failureMessage;
    }

    private void OnProviderChanged(object sender, RoutedEventArgs e)
    {
        if (PostgreSqlPanel == null)
            return;
        PostgreSqlPanel.Visibility = PostgreSqlRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private PostgreSqlConnectionSettings ReadPostgreSqlSettings()
    {
        if (!int.TryParse(PortTextBox.Text, out var port))
            throw new InvalidOperationException("Enter a valid PostgreSQL port");

        return new PostgreSqlConnectionSettings
        {
            Host = HostTextBox.Text.Trim(),
            Port = port,
            Database = DatabaseTextBox.Text.Trim(),
            Username = UsernameTextBox.Text.Trim(),
            SslMode = SslModeComboBox.SelectedItem is PostgreSqlSslMode sslMode ? sslMode : PostgreSqlSslMode.Prefer,
        };
    }

    private string ReadPassword() =>
        string.IsNullOrEmpty(PasswordInput.Password)
            ? throw new InvalidOperationException("Enter the PostgreSQL password")
            : PasswordInput.Password;

    private async void OnTestConnectionClick(object sender, RoutedEventArgs e)
    {
        await RunPostgreSqlActionAsync(async () =>
        {
            await DatabaseConfigurationService.TestPostgreSqlAsync(ReadPostgreSqlSettings(), ReadPassword());
            StatusText.Text = "Connection successful.";
        });
    }

    private async void OnContinueClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ContinueButton.IsEnabled = false;
            TestButton.IsEnabled = false;
            var settings = _settingsService.GetSettings();

            if (SqliteRadio.IsChecked == true)
            {
                if (_recoveryMode && settings.DatabaseProvider == DatabaseProvider.PostgreSql)
                {
                    var answer = MessageBox.Show(
                        "The local SQLite database has not received data since PostgreSQL was activated. Switch anyway?",
                        Title,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning
                    );
                    if (answer != MessageBoxResult.Yes)
                        return;
                }

                settings.DatabaseProvider = DatabaseProvider.Sqlite;
                settings.PendingDatabaseProvider = null;
                settings.PendingDatabaseImport = false;
                settings.PendingDatabaseSwitchId = null;
            }
            else
            {
                var postgreSql = ReadPostgreSqlSettings();
                var password = ReadPassword();
                StatusText.Text = "Testing connection…";
                await DatabaseConfigurationService.TestPostgreSqlAsync(postgreSql, password);
                _credentialStore.WritePostgreSqlPassword(password);
                settings.PostgreSql = postgreSql;

                if (_recoveryMode && settings.DatabaseProvider == DatabaseProvider.PostgreSql)
                {
                    // Recovery edits the active connection; it has already been tested successfully.
                    settings.PendingDatabaseProvider = null;
                    settings.PendingDatabaseImport = false;
                    settings.PendingDatabaseSwitchId = null;
                }
                else
                {
                    settings.PendingDatabaseProvider = DatabaseProvider.PostgreSql;
                    settings.PendingDatabaseImport = DatabaseConfigurationService.HasSqliteHistory();
                    settings.PendingDatabaseSwitchId = Guid.NewGuid().ToString("N");
                }
            }

            settings.IsFirstLaunch = false;
            _settingsService.SaveSettings(settings);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
            Log.Warning(ex, "Database setup could not be completed");
        }
        finally
        {
            ContinueButton.IsEnabled = true;
            TestButton.IsEnabled = true;
        }
    }

    private async Task RunPostgreSqlActionAsync(Func<Task> action)
    {
        try
        {
            TestButton.IsEnabled = false;
            ContinueButton.IsEnabled = false;
            StatusText.Text = "Testing connection…";
            await action();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
            Log.Debug(ex, "PostgreSQL connection test failed");
        }
        finally
        {
            TestButton.IsEnabled = true;
            ContinueButton.IsEnabled = true;
        }
    }
}
