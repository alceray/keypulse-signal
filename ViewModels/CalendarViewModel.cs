using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using KeyPulse.Helpers;
using KeyPulse.Models;
using KeyPulse.Services;
using KeyPulse.ViewModels.Calendar;

namespace KeyPulse.ViewModels;

/// <summary>
/// Transient view-model for the Calendar tab.
/// Shows a monthly grid of day tiles and a detail panel for a selected day.
/// Subscribes to AppTimerService.SecondTick for per-second realtime overlay updates.
/// </summary>
public sealed class CalendarViewModel : ToastMessageViewModelBase
{
    private readonly DailyStatsService _dailyStatsService;
    private readonly UsbMonitorService _usbMonitorService;
    private readonly IReadOnlyList<(DateTime Start, DateTime End)> _pastSessions;
    private long _selectedDayPastSeconds;
    private readonly RawInputService _rawInputService;
    private readonly AppTimerService _appTimerService;
    private readonly DeviceBuildService _builds;
    private readonly Dictionary<string, IReadOnlyList<CalendarBuildRow>> _partsCache = [];
    private DateOnly? _partsDay;
    private readonly PartsPromptService _partsPrompts;
    private bool _changingBuild;
    public ICommand EditBuildCommand { get; }
    public ICommand DeleteBuildCommand { get; }
    public ICommand ReloadBuildsCommand { get; }

    // UI-only realtime overlay state for today's row/details.
    private readonly object _liveInputLock = new();
    private readonly Dictionary<
        string,
        (long KeystrokeDelta, long MouseClickDelta, long MouseMovementDelta)
    > _todayLiveDeltaByDevice = [];
    private DateOnly _accumulatedInputDate = DateOnly.FromDateTime(DateTime.Now);
    private readonly Dictionary<string, CalendarDeviceDetail> _todayPersistedDetailByDevice = [];

    // Live second-accurate "active time" for today, seeded from each device's persisted minute total.
    private readonly ActiveSecondsAccumulator _activeSeconds = new();

    // Stable bar references per device; same list instance keeps WPF tooltip elements alive across per-second updates.
    private readonly Dictionary<string, IReadOnlyList<CalendarHourlyInputBar>> _hourlyBarsCache = [];
    private readonly SemaphoreSlim _arrowNavigationGate = new(1, 1);

    private DateOnly _currentDisplayMonth;
    private bool _isLoading;
    private CalendarDaySummary? _selectedDay;
    private bool _disposed;
    private DateOnly? _earliestDataDay;

    public ICommand PreviousMonthCommand { get; }

    public ICommand NextMonthCommand { get; }
    public ICommand SelectDayCommand { get; }

    public ObservableCollection<CalendarDaySummary> DaySummaries { get; } = [];

    public ObservableCollection<CalendarDeviceDetail> SelectedDayDetails { get; } = [];

    // Calendar grid needs leading blank cells so the first day aligns with its weekday column.
    public ObservableCollection<object?> CalendarGridItems { get; } = [];

    public string MonthTitle => _currentDisplayMonth.ToDateTime(TimeOnly.MinValue).ToString("MMMM yyyy");

    public bool CanGoNext
    {
        get
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var firstDayOfThisMonth = today.AddDays(1 - today.Day);
            return _currentDisplayMonth < firstDayOfThisMonth;
        }
    }

    public bool CanGoPrevious
    {
        get
        {
            if (_earliestDataDay == null)
                return true;
            // Disable when already on the earliest month with data.
            var currentMonthStart = _currentDisplayMonth.AddDays(1 - _currentDisplayMonth.Day);
            var earliestMonthStart = _earliestDataDay.Value.AddDays(1 - _earliestDataDay.Value.Day);
            return currentMonthStart > earliestMonthStart;
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public CalendarDaySummary? SelectedDay
    {
        get => _selectedDay;
        private set
        {
            DateOnly? selectedDay = value?.IsSelectable == true ? value.Day : null;
            ApplySelectionState(selectedDay);
            _selectedDay = selectedDay.HasValue ? DaySummaries.FirstOrDefault(s => s.Day == selectedDay.Value) : null;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedDayTitle));
            OnPropertyChanged(nameof(HasSelectedDay));
            LoadSelectedDayDetails();
        }
    }

    public bool HasSelectedDay => _selectedDay != null;

    public string SelectedDayTitle => _selectedDay != null ? _selectedDay.Day.ToString("dddd, MMMM d, yyyy") : "";

    public CalendarViewModel(
        DailyStatsService dailyStatsService,
        UsbMonitorService usbMonitorService,
        RawInputService rawInputService,
        AppTimerService appTimerService,
        DeviceBuildService builds,
        PartsPromptService partsPrompts
    )
    {
        _dailyStatsService = dailyStatsService;
        _usbMonitorService = usbMonitorService;
        _pastSessions = AppRunningIntervals.FromOrderedEvents(
            usbMonitorService.DeviceEventList.Where(e => e.EventType.IsAppEvent()).OrderBy(e => e.EventTime.ToUniversalTime()).ToArray(),
            end: null);
        _rawInputService = rawInputService;
        _appTimerService = appTimerService;
        _builds = builds;
        _partsPrompts = partsPrompts;
        EditBuildCommand = new RelayCommand(
            p => EditBuild((CalendarBuildRow)p!),
            p => p is CalendarBuildRow && !_changingBuild
        );
        DeleteBuildCommand = new RelayCommand(
            async p => await DeleteBuildAsync((CalendarBuildRow)p!),
            p => p is CalendarBuildRow && !_changingBuild
        );
        ReloadBuildsCommand = new RelayCommand(async _ => await ReloadBuildsAsync(), _ => !_changingBuild);
        _builds.Changed += PartsChanged;

        var today = DateOnly.FromDateTime(DateTime.Now);
        _currentDisplayMonth = today.AddDays(1 - today.Day); // First day of current month

        PreviousMonthCommand = new RelayCommand(_ => _ = NavigateMonthAsync(-1), _ => CanGoPrevious);
        NextMonthCommand = new RelayCommand(_ => _ = NavigateMonthAsync(1), _ => CanGoNext);
        SelectDayCommand = new RelayCommand(day => SelectDay(day as CalendarDaySummary));

        _appTimerService.SecondTick += OnSecondTick;
        _rawInputService.InputDeltaIncremented += OnInputDeltaIncremented;
        _usbMonitorService.DeviceList.CollectionChanged += DeviceList_CollectionChanged;
        foreach (var device in _usbMonitorService.DeviceList)
            device.PropertyChanged += Device_PropertyChanged;
    }

    /// <summary>Called when the tab becomes visible. Loads current month and triggers rebuild.</summary>
    public async void OnVisible()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        _currentDisplayMonth = today.AddDays(1 - today.Day);
        OnPropertyChanged(nameof(MonthTitle));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoPrevious));
        SelectedDay = null;
        var loadMonth = LoadCurrentMonthAsync(today);
        try
        {
            _earliestDataDay = await Task.Run(() => _dailyStatsService.GetEarliestDataDay());
            OnPropertyChanged(nameof(CanGoPrevious));
            CommandManager.InvalidateRequerySuggested();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to query earliest calendar data day");
        }

        await loadMonth;
    }

    /// <summary>
    /// Navigates one month forward/backward, updates command state, and reloads the visible month.
    /// </summary>
    private async Task NavigateMonthAsync(int delta, bool resetSelection = true)
    {
        _currentDisplayMonth = _currentDisplayMonth
            .AddMonths(delta)
            .AddDays(1 - _currentDisplayMonth.AddMonths(delta).Day);

        OnPropertyChanged(nameof(MonthTitle));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoPrevious));
        if (resetSelection)
            SelectedDay = null;
        await LoadCurrentMonthAsync();
    }

    /// <summary>Returns selectable day tiles in the visible month ordered from oldest to newest.</summary>
    private List<CalendarDaySummary> GetSelectableDaysInCurrentMonth()
    {
        return DaySummaries.Where(s => s.IsSelectable).OrderBy(s => s.Day).ToList();
    }

    /// <summary>
    /// Moves calendar selection by one tile in the provided direction.
    /// -1 = left (older), +1 = right (newer).
    /// </summary>
    public async Task MoveSelectionByArrowAsync(int delta)
    {
        if (delta != -1 && delta != 1)
            return;

        await _arrowNavigationGate.WaitAsync();
        try
        {
            var selectableDays = GetSelectableDaysInCurrentMonth();
            if (_selectedDay == null)
            {
                if (selectableDays.Count == 0)
                    return;

                // Start from the most recent tile in the visible month when no day is selected.
                SelectDay(selectableDays[^1]);
                return;
            }

            var selectedIndex = selectableDays.FindIndex(s => s.Day == _selectedDay.Day);
            if (selectedIndex < 0 && selectableDays.Count > 0)
            {
                // Selection can become stale when month content changes; recover inside current month first.
                SelectDay(delta < 0 ? selectableDays[^1] : selectableDays[0]);
                return;
            }

            if (selectedIndex >= 0)
            {
                var targetIndex = selectedIndex + delta;
                if (targetIndex >= 0 && targetIndex < selectableDays.Count)
                {
                    SelectDay(selectableDays[targetIndex]);
                    return;
                }
            }

            while (true)
            {
                if (delta < 0 && !CanGoPrevious)
                    return;
                if (delta > 0 && !CanGoNext)
                    return;

                await NavigateMonthAsync(delta, false);

                selectableDays = GetSelectableDaysInCurrentMonth();
                if (selectableDays.Count == 0)
                    continue;

                SelectDay(delta < 0 ? selectableDays[^1] : selectableDays[0]);
                return;
            }
        }
        finally
        {
            _arrowNavigationGate.Release();
        }
    }

    /// <summary>Selects today or a day with recorded data.</summary>
    private void SelectDay(CalendarDaySummary? day)
    {
        SelectedDay = day;
    }

    /// <summary>Loads month summaries from persistence and applies them if the view is still on that month.</summary>
    private async Task LoadCurrentMonthAsync(DateOnly? defaultDay = null)
    {
        IsLoading = true;
        var year = _currentDisplayMonth.Year;
        var month = _currentDisplayMonth.Month;

        IReadOnlyList<CalendarDaySummary> summaries;
        try
        {
            var from = new DateOnly(year, month, 1);
            var to = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
            var statRows = await Task.Run(() => _dailyStatsService.GetVisibleDailyDeviceStats(from, to));
            summaries = CalendarSummaryBuilder.BuildMonthSummaries(year, month, statRows, GetDevicesById());
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to load calendar month {Year}-{Month}", year, month);
            summaries = Array.Empty<CalendarDaySummary>();
        }

        ApplySummaries(summaries, year, month, defaultDay);
        IsLoading = false;
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>Applies month summaries to tile collections, preserves selection, and refreshes today's realtime overlay.</summary>
    private void ApplySummaries(IReadOnlyList<CalendarDaySummary> summaries, int year, int month, DateOnly? defaultDay)
    {
        // Only apply if still on the same month (user may have navigated away).
        if (year != _currentDisplayMonth.Year || month != _currentDisplayMonth.Month)
            return;

        var selectedDay = _selectedDay?.Day ?? defaultDay;
        DaySummaries.Clear();
        CalendarGridItems.Clear();

        // Leading blank tiles so day 1 aligns with its weekday (Sunday = 0).
        var firstDay = new DateTime(year, month, 1);
        var leadingBlanks = (int)firstDay.DayOfWeek;
        for (var i = 0; i < leadingBlanks; i++)
            CalendarGridItems.Add(null);

        foreach (var summary in summaries)
        {
            var styledSummary = CloneSummary(summary, summary.Day == selectedDay);

            DaySummaries.Add(styledSummary);
            CalendarGridItems.Add(styledSummary);
        }

        // Apply in-memory realtime overlay immediately after baseline load.
        ApplyRealtimeTodayOverlay();

        // Refresh selected day tile if it lives in this month.
        if (selectedDay is { } day && day.Year == year && day.Month == month)
        {
            var refreshed = DaySummaries.FirstOrDefault(s => s.Day == day);
            if (refreshed != null)
                SelectedDay = refreshed;
        }
    }

    /// <summary>Loads per-device detail rows for the selected day and caches today's persisted baseline for live overlays.</summary>
    private async void LoadSelectedDayDetails()
    {
        SelectedDayDetails.Clear();
        _todayPersistedDetailByDevice.Clear();
        _hourlyBarsCache.Clear();
        _partsCache.Clear();
        if (_selectedDay == null || !_selectedDay.HasData)
            return;

        var day = _selectedDay.Day;
        var isToday = day == DateOnly.FromDateTime(DateTime.Now);
        var start = TimeFormatter.LocalDayToUtc(day);
        var end = TimeFormatter.LocalDayToUtc(day.AddDays(1));
        _selectedDayPastSeconds = _pastSessions.Sum(session => OverlapSeconds(
            session.Start.ToUniversalTime(), session.End.ToUniversalTime(), start, end));
        IReadOnlyList<CalendarDeviceDetail> details;
        try
        {
            var statRows = await Task.Run(() => _dailyStatsService.GetVisibleDailyDeviceStats(day, day));
            details = CalendarSummaryBuilder.BuildDayDetails(statRows, GetDevicesById(), isToday);
            await _builds.LoadAsync();
            _partsCache.Clear();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to load calendar day detail for {Day}", day);
            details = Array.Empty<CalendarDeviceDetail>();
        }

        if (_selectedDay?.Day != day)
            return;

        SelectedDayDetails.Clear();
        var runningSeconds = AppRunningSeconds(day);
        foreach (var d in OrderDetailsForDisplay(details))
            SelectedDayDetails.Add(WithBuilds(d with { AppRunningSeconds = runningSeconds }, day));

        var today = DateOnly.FromDateTime(DateTime.Now);
        if (day != today)
            return;

        foreach (var d in details)
            _todayPersistedDetailByDevice[d.DeviceId] = d;

        // Establish today before seeding so a later rollover check can't wipe the baselines.
        lock (_liveInputLock)
        {
            _activeSeconds.ResetIfDayChanged(today);
            foreach (var d in details)
                _activeSeconds.Seed(d.DeviceId, d.ActiveSeconds);
        }

        ApplyRealtimeTodayOverlay();
    }

    /// <summary>Refreshes today's realtime overlay every second while preserving persisted baselines.</summary>
    private void OnSecondTick(object? sender, EventArgs e)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var dayRolledOver = ResetLiveInputOverlayIfDayChanged(today);
        if (dayRolledOver)
        {
            _ = LoadCurrentMonthAsync(); // clears yesterday's connected highlights
            return;
        }

        RequestOverlayRefresh();
    }

    /// <summary>Accumulates in-memory input deltas for today and schedules a UI overlay refresh.</summary>
    private void OnInputDeltaIncremented(
        string deviceId,
        (long KeystrokeDelta, long MouseClickDelta, long MouseMovementDelta) delta
    )
    {
        if (delta.KeystrokeDelta + delta.MouseClickDelta + delta.MouseMovementDelta <= 0)
            return;

        var today = DateOnly.FromDateTime(DateTime.Now);
        ResetLiveInputOverlayIfDayChanged(today);
        lock (_liveInputLock)
        {
            _todayLiveDeltaByDevice[deviceId] = _todayLiveDeltaByDevice.TryGetValue(deviceId, out var current)
                ? (
                    current.KeystrokeDelta + delta.KeystrokeDelta,
                    current.MouseClickDelta + delta.MouseClickDelta,
                    current.MouseMovementDelta + delta.MouseMovementDelta
                )
                : delta;
            _activeSeconds.RecordActivity(deviceId, DateTime.Now);
        }

        // Tile connected-state doesn't change on keystrokes; skip the tile rebuild.
        RequestOverlayRefresh(updateTileSummary: false);
    }

    /// <summary>Resets in-memory live counters when local time crosses into a new day.</summary>
    private bool ResetLiveInputOverlayIfDayChanged(DateOnly today)
    {
        lock (_liveInputLock)
        {
            if (_accumulatedInputDate == today)
                return false;

            _todayLiveDeltaByDevice.Clear();
            _activeSeconds.ResetIfDayChanged(today);
            _accumulatedInputDate = today;
            return true;
        }
    }

    /// <summary>Runs realtime overlay updates on the UI dispatcher when available.</summary>
    private void RequestOverlayRefresh(bool updateTileSummary = true)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (!ShutdownDispose.IsDispatcherUsable(dispatcher))
            return;

        if (dispatcher!.CheckAccess())
            ApplyRealtimeTodayOverlay(updateTileSummary);
        else
            // Raw input and timer callbacks may arrive off-thread; marshal to UI safely.
            dispatcher.BeginInvoke(new Action(() => ApplyRealtimeTodayOverlay(updateTileSummary)));
    }

    /// <summary>
    /// Applies UI-only realtime overlay for today's tile and selected-today detail panel.
    /// Persistence remains in source-table write paths (DeviceEvents/ActivitySnapshots).
    /// </summary>
    private void ApplyRealtimeTodayOverlay(bool updateTileSummary = true)
    {
        var now = DateTimeOffset.Now;
        var today = DateOnly.FromDateTime(now.DateTime);

        if (today.Year != _currentDisplayMonth.Year || today.Month != _currentDisplayMonth.Month)
            return;

        var persisted = DaySummaries.FirstOrDefault(d => d.Day == today);
        if (persisted == null)
            return;

        var selectedToday = _selectedDay?.Day == today;
        if (!updateTileSummary && !selectedToday)
            return;

        Dictionary<string, long>? connectionOverlayByDevice = null;
        if (selectedToday)
        {
            var midnight = TimeFormatter.LocalDayToUtc(today);
            // Connection overlay: open sessions contribute elapsed seconds since max(sessionStart, local midnight).
            connectionOverlayByDevice = new Dictionary<string, long>();
            foreach (var device in _usbMonitorService.DeviceList)
            {
                if (!device.IsConnected || device.IsHiddenFromDisplay)
                    continue;

                var startLocal = TimeFormatter.ToLocalTime(device.SessionStartedAt!.Value);
                var seconds = LiveConnectionSeconds(startLocal, midnight, now);
                if (seconds <= 0)
                    continue;

                connectionOverlayByDevice[device.DeviceId] = seconds;
            }
        }

        if (updateTileSummary)
        {
            var hiddenDeviceIds = _usbMonitorService
                .DeviceList.Where(d => d.IsHiddenFromDisplay)
                .Select(d => d.DeviceId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var byId = persisted
                .Devices.Where(d => !hiddenDeviceIds.Contains(d.DeviceId))
                .ToDictionary(
                    d => d.DeviceId,
                    d => new CalendarTileDevice
                    {
                        DeviceId = d.DeviceId,
                        DeviceName = d.DeviceName,
                        DeviceType = d.DeviceType,
                        IsConnected = false,
                    }
                );
            foreach (var device in _usbMonitorService.DeviceList.Where(d => d.IsConnected && !d.IsHiddenFromDisplay))
            {
                byId[device.DeviceId] = new CalendarTileDevice
                {
                    DeviceId = device.DeviceId,
                    DeviceName = device.DeviceName,
                    DeviceType = device.DeviceType,
                    IsConnected = true,
                };
            }

            var todaySummary = new CalendarDaySummary
            {
                Day = persisted.Day,
                HasData = persisted.HasData || byId.Values.Any(d => d.IsConnected),
                IsSelected = selectedToday,
                Devices = byId
                    .Values.OrderByDescending(d => d.IsConnected)
                    .ThenBy(d =>
                        d.DeviceType == DeviceTypes.Keyboard ? 0
                        : d.DeviceType == DeviceTypes.Mouse ? 1
                        : 2
                    )
                    .ThenBy(d => d.DeviceName)
                    .ToList(),
            };

            for (var i = 0; i < DaySummaries.Count; i++)
            {
                if (DaySummaries[i].Day == todaySummary.Day)
                {
                    DaySummaries[i] = todaySummary;
                    break;
                }
            }

            for (var i = 0; i < CalendarGridItems.Count; i++)
            {
                if (CalendarGridItems[i] is CalendarDaySummary existing && existing.Day == todaySummary.Day)
                {
                    CalendarGridItems[i] = todaySummary;
                    break;
                }
            }
        }

        if (selectedToday)
        {
            Dictionary<
                string,
                (long KeystrokeDelta, long MouseClickDelta, long MouseMovementDelta)
            > liveOverlayByDevice;
            lock (_liveInputLock)
                liveOverlayByDevice = _todayLiveDeltaByDevice.ToDictionary(k => k.Key, v => v.Value);

            ApplyRealtimeTodayDetailOverlay(connectionOverlayByDevice!, liveOverlayByDevice);
        }
    }

    /// <summary>Synchronizes selected-state styling in both summary and grid collections.</summary>
    private void ApplySelectionState(DateOnly? selectedDay)
    {
        UpdateSelectionInCollection(DaySummaries, selectedDay);
        UpdateSelectionInGrid(selectedDay);
    }

    /// <summary>Replaces only rows whose selection flag changed to trigger UI updates efficiently.</summary>
    private static void UpdateSelectionInCollection(IList<CalendarDaySummary> summaries, DateOnly? selectedDay)
    {
        for (var i = 0; i < summaries.Count; i++)
        {
            var existing = summaries[i];
            var shouldBeSelected = selectedDay.HasValue && existing.Day == selectedDay.Value;
            if (existing.IsSelected == shouldBeSelected)
                continue;

            summaries[i] = CloneSummary(existing, shouldBeSelected);
        }
    }

    /// <summary>Mirrors selection changes into the mixed grid list that includes leading blank cells.</summary>
    private void UpdateSelectionInGrid(DateOnly? selectedDay)
    {
        for (var i = 0; i < CalendarGridItems.Count; i++)
        {
            if (CalendarGridItems[i] is not CalendarDaySummary existing)
                continue;

            var shouldBeSelected = selectedDay.HasValue && existing.Day == selectedDay.Value;
            if (existing.IsSelected == shouldBeSelected)
                continue;

            CalendarGridItems[i] = CloneSummary(existing, shouldBeSelected);
        }
    }

    /// <summary>
    /// Snapshot of the in-memory device list keyed by id, for resolving names/types in builders.
    /// Must be called on the UI thread (DeviceList is mutated there).
    /// </summary>
    private Dictionary<string, Device> GetDevicesById()
    {
        var byId = new Dictionary<string, Device>();
        foreach (var device in _usbMonitorService.DeviceList)
            byId[device.DeviceId] = device;
        return byId;
    }

    /// <summary>Creates a shallow summary copy with explicit selection state for immutable-style updates.</summary>
    private static CalendarDaySummary CloneSummary(CalendarDaySummary summary, bool isSelected)
    {
        return new CalendarDaySummary
        {
            Day = summary.Day,
            HasData = summary.HasData,
            IsSelected = isSelected,
            Devices = summary.Devices,
        };
    }

    /// <summary>Applies live connection/input overlays to today's detail rows without mutating persisted data.</summary>
    private void ApplyRealtimeTodayDetailOverlay(
        IReadOnlyDictionary<string, long> connectionOverlayByDevice,
        IReadOnlyDictionary<
            string,
            (long KeystrokeDelta, long MouseClickDelta, long MouseMovementDelta)
        > liveOverlayByDevice
    )
    {
        if (_selectedDay == null)
            return;

        var ids = new HashSet<string>(_todayPersistedDetailByDevice.Keys);
        foreach (var id in connectionOverlayByDevice.Keys)
            ids.Add(id);
        foreach (var id in liveOverlayByDevice.Keys)
            ids.Add(id);
        // Union persisted + connected-now + live-input IDs so detail rows stay complete for today's view.

        var rows = new List<CalendarDeviceDetail>(ids.Count);
        var runningSeconds = AppRunningSeconds(_selectedDay.Day);
        foreach (var id in ids)
        {
            _todayPersistedDetailByDevice.TryGetValue(id, out var persisted);
            var device = _usbMonitorService.DeviceList.FirstOrDefault(d => d.DeviceId == id);
            if (device?.IsHiddenFromDisplay == true)
                continue;

            var connectionOverlay = connectionOverlayByDevice.TryGetValue(id, out var sec) ? sec : 0L;
            var liveDelta = liveOverlayByDevice.TryGetValue(id, out var overlay)
                ? overlay
                : (KeystrokeDelta: 0L, MouseClickDelta: 0L, MouseMovementDelta: 0L);
            var sessionCount = (persisted?.SessionCount ?? 0) + (device?.IsConnected == true ? 1 : 0);

            var baseConnection = persisted?.ConnectionSeconds ?? 0L;

            var persistedActiveSeconds = persisted?.ActiveSeconds ?? 0L;
            long activeSeconds;
            lock (_liveInputLock)
            {
                _activeSeconds.Seed(id, persistedActiveSeconds);
                // Persisted floor guards against a freshly reset accumulator after a restart or rollover.
                activeSeconds = Math.Max(persistedActiveSeconds, _activeSeconds.GetActiveSeconds(id));
            }
            // Active time can never exceed connected time.
            activeSeconds = Math.Min(activeSeconds, baseConnection + connectionOverlay);

            // Same reference kept so WPF ItemsControl doesn't recreate bar elements (tooltip-stable).
            if (!_hourlyBarsCache.TryGetValue(id, out var stableBars))
                _hourlyBarsCache[id] = stableBars =
                    persisted?.HourlyInputBars ?? CalendarHourlyInputBarBuilder.Build(new long[24]);

            rows.Add(
                new CalendarDeviceDetail
                {
                    DeviceId = id,
                    DeviceName = persisted?.DeviceName ?? device?.DeviceName ?? id,
                    DeviceType = persisted?.DeviceType ?? device?.DeviceType ?? DeviceTypes.Unknown,
                    IsConnected = device?.IsConnected == true,
                    // This overlay only runs for today's selected row, whose totals tick live.
                    IsToday = true,
                    SessionCount = sessionCount,
                    ConnectionSeconds = baseConnection + connectionOverlay,
                    Keystrokes = (persisted?.Keystrokes ?? 0) + liveDelta.KeystrokeDelta,
                    MouseClicks = (persisted?.MouseClicks ?? 0) + liveDelta.MouseClickDelta,
                    ActiveSeconds = activeSeconds,
                    AppRunningSeconds = runningSeconds,
                    HourlyInputBars = stableBars,
                }
            );
        }

        var ordered = OrderDetailsForDisplay(rows).ToList();
        if (_selectedDay != null)
            for (var i = 0; i < ordered.Count; i++)
                ordered[i] = WithBuilds(ordered[i], _selectedDay.Day);

        // Update in-place when order is unchanged to keep tooltip targets alive.
        var canUpdateInPlace =
            ordered.Count == SelectedDayDetails.Count
            && ordered.Select(r => r.DeviceId).SequenceEqual(SelectedDayDetails.Select(r => r.DeviceId));

        if (canUpdateInPlace)
        {
            for (var i = 0; i < ordered.Count; i++)
                SelectedDayDetails[i] = ordered[i];
        }
        else
        {
            SelectedDayDetails.Clear();
            foreach (var row in ordered)
                SelectedDayDetails.Add(row);
        }
    }

    /// <summary>Applies consistent display ordering: connected first, then keyboard/mouse/unknown, then name.</summary>
    private static IEnumerable<CalendarDeviceDetail> OrderDetailsForDisplay(IEnumerable<CalendarDeviceDetail> rows)
    {
        return rows.OrderByDescending(r => r.IsConnected)
            .ThenBy(r =>
                r.DeviceType switch
                {
                    DeviceTypes.Keyboard => 0,
                    DeviceTypes.Mouse => 1,
                    _ => 2,
                }
            )
            .ThenBy(r => r.DeviceName);
    }

    private void DeviceList_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (Device device in e.NewItems)
                device.PropertyChanged += Device_PropertyChanged;

        if (e.OldItems != null)
            foreach (Device device in e.OldItems)
                device.PropertyChanged -= Device_PropertyChanged;
    }

    private void Device_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Device.IsHiddenFromDisplay) or nameof(Device.DeviceType))
            _ = LoadCurrentMonthAsync();
    }

    public override void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _builds.Changed -= PartsChanged;
        _appTimerService.SecondTick -= OnSecondTick;
        _rawInputService.InputDeltaIncremented -= OnInputDeltaIncremented;
        _usbMonitorService.DeviceList.CollectionChanged -= DeviceList_CollectionChanged;
        foreach (var device in _usbMonitorService.DeviceList)
            device.PropertyChanged -= Device_PropertyChanged;
        _arrowNavigationGate.Dispose();
        base.Dispose();
    }

    internal static long LiveConnectionSeconds(DateTimeOffset connected, DateTimeOffset midnight, DateTimeOffset now)
    {
        var start = connected < midnight ? midnight : connected;
        return Math.Max(0, (long)(now - start).TotalSeconds);
    }

    private long AppRunningSeconds(DateOnly day)
    {
        var start = _usbMonitorService.AppSessionStartedAt;
        return start == default ? _selectedDayPastSeconds : _selectedDayPastSeconds + OverlapSeconds(
            start.ToUniversalTime(), DateTime.UtcNow, TimeFormatter.LocalDayToUtc(day), TimeFormatter.LocalDayToUtc(day.AddDays(1)));
    }

    internal static long OverlapSeconds(DateTime start, DateTime end, DateTime dayStart, DateTime dayEnd) =>
        Math.Max(0, Math.Min(end.Ticks, dayEnd.Ticks) - Math.Max(start.Ticks, dayStart.Ticks)) / TimeSpan.TicksPerSecond;

    private CalendarDeviceDetail WithBuilds(CalendarDeviceDetail detail, DateOnly day)
    {
        if (detail.DeviceType != DeviceTypes.Keyboard)
            return detail;
        if (_partsDay != day)
        {
            _partsCache.Clear();
            _partsDay = day;
        }
        if (!_partsCache.TryGetValue(detail.DeviceId, out var rows))
        {
            var saved = _builds.GetDay(detail.DeviceId, day);
            rows =
                saved == null
                    ? []
                    : saved.Builds.Select((build, index) => new CalendarBuildRow(saved, build, index + 1)).ToArray();
            _partsCache[detail.DeviceId] = rows;
        }
        return detail with
        {
            Builds = rows,
            BuildsMessage = _builds.LoadError ?? (rows.Count == 0 ? "No build recorded" : ""),
        };
    }

    private void PartsChanged(string id, DateOnly day)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (!ShutdownDispose.IsDispatcherUsable(dispatcher))
            return;
        dispatcher!.BeginInvoke(() =>
        {
            if (_disposed || _selectedDay?.Day != day)
                return;
            _partsCache.Remove(id);
            for (var i = 0; i < SelectedDayDetails.Count; i++)
                if (SelectedDayDetails[i].DeviceId == id)
                    SelectedDayDetails[i] = WithBuilds(SelectedDayDetails[i], day);
        });
    }

    private void EditBuild(CalendarBuildRow row)
    {
        var device = _usbMonitorService.DeviceList.FirstOrDefault(x => x.DeviceId == row.Day.DeviceId);
        if (device != null)
            _partsPrompts.OpenEditor(device, row.Day.Day, row.Build.BuildKey);
    }

    private async Task DeleteBuildAsync(CalendarBuildRow row)
    {
        var name =
            _usbMonitorService.DeviceList.FirstOrDefault(x => x.DeviceId == row.Day.DeviceId)?.DeviceName
            ?? row.Day.DeviceId;
        if (
            MessageBox.Show(
                $"Delete {row.Label} for {name} on {row.Day.Day:yyyy-MM-dd}?",
                "Delete build",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            ) != MessageBoxResult.Yes
        )
            return;
        _changingBuild = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            var result = await _builds.DeleteAsync(row.Day, row.Build.BuildKey);
            if (!_disposed)
                ToastMessage = result.Succeeded ? "Build deleted" : result.Error ?? "Could not delete build.";
        }
        finally
        {
            _changingBuild = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task ReloadBuildsAsync()
    {
        _changingBuild = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            await _builds.LoadAsync(retry: true);
            if (_disposed || _selectedDay == null)
                return;
            _partsCache.Clear();
            for (var i = 0; i < SelectedDayDetails.Count; i++)
                if (SelectedDayDetails[i].DeviceType == DeviceTypes.Keyboard)
                    SelectedDayDetails[i] = WithBuilds(SelectedDayDetails[i], _selectedDay.Day);
            if (_builds.LoadError != null)
                ToastMessage = _builds.LoadError;
        }
        finally
        {
            _changingBuild = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
