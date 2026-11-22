using KeyPulse.Models;
using KeyPulse.Tests.Infrastructure;
using KeyPulse.Tests.Services;
using KeyPulse.ViewModels;
using KeyPulse.ViewModels.Calendar;

namespace KeyPulse.Tests.ViewModels;

public class DevicePartsTests
{
    [Fact]
    public void Devices_AppendsOnlyWhileConnected_AndPreservesDraftAcrossReconnect() =>
        PartsViewModelTests.Run(async () =>
        {
            using var db = new SqliteTestDatabase();
            using var scope = new DeviceBuildTests.BuildScope(db.Factory);
            var today = DateOnly.FromDateTime(DateTime.Now);
            using var editor = new DevicePartsEditorViewModel(
                scope.Device,
                scope.Builds,
                scope.Collection,
                scope.Timer,
                today
            );
            await editor.ReloadAsync();
            await PartsViewModelTests.Pump();
            editor.HasConflict.ShouldBeFalse();
            editor.Choices.Count.ShouldBe(2);
            editor.Choices.Single(x => x.Part.ItemKey == scope.First.ItemKey).IsSelected = true;
            scope.SetConnected(false);
            await PartsViewModelTests.Pump();
            editor.HasConflict.ShouldBeFalse();
            editor.HasChanges.ShouldBeTrue();
            editor.SaveCommand.CanExecute(null).ShouldBeFalse();
            await editor.SaveAsync();
            scope.Builds.GetDay(scope.Device.DeviceId, today)!.Builds.ShouldBeEmpty();
            scope.SetConnected(true);
            await PartsViewModelTests.Pump();
            editor.SaveCommand.CanExecute(null).ShouldBeTrue();
            await editor.SaveAsync();
            editor.HasConflict.ShouldBeFalse();
            editor.HasChanges.ShouldBeFalse();
            var first = scope.Builds.GetCurrent(scope.Device.DeviceId, today)!;
            editor.Choices.Single(x => x.Part.ItemKey == scope.Second.ItemKey).IsSelected = true;
            await editor.SaveAsync();
            var day = scope.Builds.GetDay(scope.Device.DeviceId, today)!;
            day.Builds.Length.ShouldBe(2);
            day.Builds[0].Parts.ShouldBe(first.Parts);
            day.Current!.Parts.Length.ShouldBe(2);
        });

    [Fact]
    public void Calendar_EditStaysPinned_ConflictsRetainDraft_AndRemovedPartsRemain() =>
        PartsViewModelTests.Run(async () =>
        {
            using var db = new SqliteTestDatabase();
            using var scope = new DeviceBuildTests.BuildScope(db.Factory);
            var day = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
            scope
                .Data.SaveDeviceBuild(scope.Device.DeviceId, day, null, null, [scope.First.ItemKey], day)
                .Changed.ShouldBeTrue();
            await scope.Builds.LoadAsync();
            var captured = scope.Builds.GetDay(scope.Device.DeviceId, day)!;
            var key = captured.Current!.BuildKey;
            var clock = day.AddDays(1);
            using var editor = new DevicePartsEditorViewModel(
                scope.Device,
                scope.Builds,
                scope.Collection,
                scope.Timer,
                day,
                key,
                () => clock
            );
            scope.SetConnected(false);
            await editor.ReloadAsync();
            editor.Choices.Single(x => x.Part.ItemKey == scope.Second.ItemKey).IsSelected = true;
            clock = clock.AddDays(1);
            await editor.SaveAsync();
            editor.Day.ShouldBe(day);
            var changed = scope.Builds.GetDay(scope.Device.DeviceId, day)!;
            changed.Builds.Length.ShouldBe(1);
            changed.Current!.BuildKey.ShouldBe(key);
            changed.Current.Parts.Length.ShouldBe(2);
            editor.Choices.Single(x => x.Part.ItemKey == scope.First.ItemKey).IsSelected = false;
            (
                await scope.Builds.SaveAsync(scope.Device.DeviceId, day, changed, key, [scope.First.ItemKey])
            ).Changed.ShouldBeTrue();
            await PartsViewModelTests.Pump();
            editor.HasConflict.ShouldBeTrue();
            editor.HasChanges.ShouldBeTrue();
            editor.SaveCommand.CanExecute(null).ShouldBeFalse();
            await editor.SaveAsync();
            editor.Choices.Single(x => x.Part.ItemKey == scope.Second.ItemKey).IsSelected.ShouldBeTrue();
            using (var context = db.CreateContext())
            {
                context.CollectionItems.Remove(context.CollectionItems.Single(x => x.ItemKey == scope.First.ItemKey));
                context.SaveChanges();
            }
            await editor.ReloadAsync();
            editor.HasConflict.ShouldBeFalse();
            editor.HasChanges.ShouldBeFalse();
            var removed = editor.Choices.Single(x => x.Part.ItemKey == scope.First.ItemKey);
            removed.IsSelected.ShouldBeTrue();
            removed.Status.ShouldBe("Removed from collection");
            removed.IsSelected = false;
            await editor.SaveAsync();
            scope.Builds.GetCurrent(scope.Device.DeviceId, day)!.Parts.ShouldBeEmpty();
            var finalDay = scope.Builds.GetDay(scope.Device.DeviceId, day)!;
            (await scope.Builds.DeleteAsync(finalDay, key)).Changed.ShouldBeTrue();
            await editor.ReloadAsync();
            editor.CanEdit.ShouldBeFalse();
            editor.ToastMessage.ShouldContain("deleted");
        });

    [Fact]
    public void Devices_MidnightKeepsDraftUntilExplicitReload() =>
        PartsViewModelTests.Run(async () =>
        {
            using var db = new SqliteTestDatabase();
            using var scope = new DeviceBuildTests.BuildScope(db.Factory);
            var actual = DateOnly.FromDateTime(DateTime.Now);
            var clock = actual;
            using var editor = new DevicePartsEditorViewModel(
                scope.Device,
                scope.Builds,
                scope.Collection,
                scope.Timer,
                actual,
                today: () => clock
            );
            await editor.ReloadAsync();
            editor.Choices[0].IsSelected = true;
            clock = actual.AddDays(1);
            await editor.SaveAsync();
            editor.Day.ShouldBe(actual);
            editor.HasConflict.ShouldBeTrue();
            editor.HasChanges.ShouldBeTrue();
            scope.Builds.GetCurrent(scope.Device.DeviceId, actual).ShouldBeNull();
            await editor.ReloadAsync();
            editor.Day.ShouldBe(clock);
            editor.SaveCommand.CanExecute(null).ShouldBeFalse(); // The persistence clock also rejects future-day adds.
        });

    [Fact]
    public void Editor_ReadAndWriteFailuresRetainDraft_AndRetryRecovers() =>
        PartsViewModelTests.Run(async () =>
        {
            using var db = new SqliteTestDatabase();
            var factory = new CollectionPersistenceTests.FaultFactory(db.Factory);
            using var scope = new DeviceBuildTests.BuildScope(factory);
            using var editor = new DevicePartsEditorViewModel(
                scope.Device,
                scope.Builds,
                scope.Collection,
                scope.Timer,
                DateOnly.FromDateTime(DateTime.Now)
            );
            factory.FailRead = true;
            await editor.ReloadAsync();
            editor.NeedsRetry.ShouldBeTrue();
            editor.SaveCommand.CanExecute(null).ShouldBeFalse();
            factory.FailRead = false;
            await editor.ReloadAsync();
            editor.NeedsRetry.ShouldBeFalse();
            editor.Choices[0].IsSelected = true;
            factory.FailAfterSave = true;
            await editor.SaveAsync();
            editor.HasChanges.ShouldBeTrue();
            editor.Choices[0].IsSelected.ShouldBeTrue();
            factory.FailAfterSave = false;
            await editor.SaveAsync();
            editor.HasChanges.ShouldBeFalse();
        });

    [Theory]
    [InlineData(2026, 3, 8)]
    [InlineData(2026, 11, 1)]
    public async Task Calendar_UsesSavedLocalDates_AndStableKeysAfterDeletion(int year, int month, int date)
    {
        using var db = new SqliteTestDatabase();
        using var scope = new DeviceBuildTests.BuildScope(db.Factory);
        var day = new DateOnly(year, month, date);
        var id = scope.Device.DeviceId;
        scope.Data.SaveDeviceBuild(id, day, null, null, [scope.First.ItemKey], day).Changed.ShouldBeTrue();
        var saved = scope.Data.ReadDeviceBuildDays().Single();
        scope.Data.SaveDeviceBuild(id, day, saved, null, [scope.Second.ItemKey], day).Changed.ShouldBeTrue();
        await scope.Builds.LoadAsync();
        saved = scope.Builds.GetDay(id, day)!;
        saved.Day.ShouldBe(day);
        var rows = saved.Builds.Select((build, index) => new CalendarBuildRow(saved, build, index + 1)).ToArray();
        rows[0].Label.ShouldBe("Build 1");
        rows[1].Label.ShouldContain("Current");
        (await scope.Builds.DeleteAsync(saved, rows[0].Build.BuildKey)).Changed.ShouldBeTrue();
        var remaining = scope.Builds.GetDay(id, day)!;
        new CalendarBuildRow(remaining, remaining.Current!, 1).Label.ShouldContain("Build 1");
        remaining.Current!.BuildKey.ShouldBe(rows[1].Build.BuildKey);
        scope.Builds.GetDay(id, day.AddDays(1)).ShouldBeNull();
        using var context = db.CreateContext();
        context.DailyDeviceStats.Count().ShouldBe(0);
    }
}
