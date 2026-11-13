using KeyPulse.Helpers;
using KeyPulse.Models;
using KeyPulse.Services;
using KeyPulse.Tests.Infrastructure;
using KeyPulse.ViewModels;
using KeyPulse.ViewModels.Settings;

namespace KeyPulse.Tests.ViewModels;

public class SettingsViewModelTests
{
    [Theory]
    [InlineData(null, "legacy-secret")]
    [InlineData("11111111111111111111111111111111", "active-secret")]
    [InlineData("22222222222222222222222222222222", "")]
    public void PasswordPrefill_UsesTheDisplayedConnectionCredential(string? reference, string expected)
    {
        using var scope = new DatabaseSwitchTestScope();
        scope.Credentials.Password = "legacy-secret";
        scope.Credentials.Entries["11111111111111111111111111111111"] = "active-secret";
        scope.Credentials.Entries["33333333333333333333333333333333"] = "destination-secret";
        scope.Settings.SaveSettings(
            new AppUserSettings
            {
                DatabaseProvider = DatabaseProvider.PostgreSql,
                PostgreSql = new() { Database = "source" },
                PostgreSqlCredentialReference = reference,
                PendingDatabaseProvider = DatabaseProvider.PostgreSql,
                PendingPostgreSql = new() { Database = "destination" },
                PendingPostgreSqlCredentialReference = "33333333333333333333333333333333",
            }
        );
        using var timer = new AppTimerService();
        using var updates = new UpdateService(timer);
        using var viewModel = new SettingsViewModel(
            scope.Settings,
            new StartupRegistrationService(),
            updates,
            scope.Credentials
        );

        viewModel.PostgreSqlDatabase.ShouldBe("source");
        viewModel.PostgreSqlPassword.ShouldBe(expected);
    }

    [Fact]
    public async Task CancelEdits_RestoresTheSavedPasswordAndNotifiesTheView()
    {
        using var scope = new DatabaseSwitchTestScope();
        scope.Credentials.Password = "saved-secret";
        scope.Settings.SaveSettings(
            new AppUserSettings
            {
                DatabaseProvider = DatabaseProvider.PostgreSql,
                PostgreSql = new() { Database = "source" },
            }
        );
        using var timer = new AppTimerService();
        using var updates = new UpdateService(timer);
        using var viewModel = new SettingsViewModel(
            scope.Settings,
            new StartupRegistrationService(),
            updates,
            scope.Credentials
        );
        viewModel.BeginEditConnection();
        viewModel.PostgreSqlPassword.ShouldBe("saved-secret");
        viewModel.PostgreSqlDatabase = "destination";
        viewModel.PostgreSqlPassword = "edited-secret";
        string? notifiedPassword = null;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SettingsViewModel.PostgreSqlPassword))
                notifiedPassword = viewModel.PostgreSqlPassword;
        };

        await viewModel.CancelDatabaseChangesAsync();

        viewModel.PostgreSqlDatabase.ShouldBe("source");
        viewModel.PostgreSqlPassword.ShouldBe("saved-secret");
        notifiedPassword.ShouldBe("saved-secret");
        viewModel.BeginEditConnection();
        viewModel.PostgreSqlPassword.ShouldBe("saved-secret");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClearingPassword_RequiresEntryBeforeTestingOrSaving(bool save)
    {
        using var scope = new DatabaseSwitchTestScope();
        scope.Credentials.Password = "saved-secret";
        scope.Settings.SaveSettings(new AppUserSettings { DatabaseProvider = DatabaseProvider.PostgreSql });
        using var timer = new AppTimerService();
        using var updates = new UpdateService(timer);
        using var viewModel = new SettingsViewModel(
            scope.Settings,
            new StartupRegistrationService(),
            updates,
            scope.Credentials
        );
        viewModel.BeginEditConnection();
        viewModel.PostgreSqlPassword = "";

        var command = (AsyncRelayCommand)(
            save ? viewModel.SaveAndRestartDatabaseCommand : viewModel.TestDatabaseConnectionCommand
        );
        await command.ExecuteAsync(null);

        viewModel.ToastMessage.ShouldContain("Enter the PostgreSQL password");
        scope.Settings.ReadPersistedSettings().PendingDatabaseProvider.ShouldBeNull();
        scope.Credentials.Password.ShouldBe("saved-secret");
    }
}

public class RetentionOptionsTests
{
    [Fact]
    public void All_OrdersShortestToLongestWithForeverLast()
    {
        RetentionOptions.All.Select(o => o.Months).ShouldBe([6, 12, 24, 0]);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(6, 6)]
    [InlineData(12, 12)]
    [InlineData(24, 24)]
    public void FromMonths_KnownValue_ReturnsMatchingOption(int months, int expected) =>
        RetentionOptions.FromMonths(months).Months.ShouldBe(expected);

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(48)]
    public void FromMonths_UnknownValue_FallsBackToForever(int months) =>
        RetentionOptions.FromMonths(months).Months.ShouldBe(0);
}
