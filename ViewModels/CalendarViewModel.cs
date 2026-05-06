using System.Collections.ObjectModel;
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
public sealed class CalendarViewModel : ObservableObject, IDisposable
{
    private readonly DailyStatsService _dailyStatsService;
    private readonly UsbMonitorService _usbMonitorService;
    private readonly RawInputService _rawInputService;
    private readonly AppTimerService _appTimerService;

    // UI-only realtime overlay state for today's row/details.
    private readonly object _liveInputLock = new();
    private readonly Dictionary<
        string,
        (long KeystrokeDelta, long MouseClickDelta, long MouseMovementDelta)
    > _todayLiveDeltaByDevice = [];
    private long _todayLiveInputDeltaTotal;
    private DateOnly _accumulatedInputDate = DateOnly.FromDateTime(DateTime.Now);
    private readonly Dictionary<string, CalendarDeviceDetail> _todayPersistedDetailByDevice = [];
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
            DateOnly? selectedDay = value?.HasData == true ? value.Day : null;
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
        AppTimerService appTimerService
    )
    {
        _dailyStatsService = dailyStatsService;
        _usbMonitorService = usbMonitorService;
        _rawInputService = rawInputService;
        _appTimerService = appTimerService;

        var today = DateOnly.FromDateTime(DateTime.Now);
        _currentDisplayMonth = today.AddDays(1 - today.Day); // First day of current month

        PreviousMonthCommand = new RelayCommand(_ => _ = NavigateMonthAsync(-1), _ => CanGoPrevious);
        NextMonthCommand = new RelayCommand(_ => _ = NavigateMonthAsync(1), _ => CanGoNext);
        SelectDayCommand = new RelayCommand(day => SelectDay(day as CalendarDaySummary));

        _appTimerService.SecondTick += OnSecondTick;
        _rawInputService.InputDeltaIncremented += OnInputDeltaIncremented;
    }

    /// <summary>Called when the tab becomes visible. Loads current month and triggers rebuild.</summary>
    public async void OnVisible()
    {
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

        _ = LoadCurrentMonthAsync();
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

    /// <summary>Returns data-bearing day tiles in the visible month ordered from oldest to newest.</summary>
    private List<CalendarDaySummary> GetSelectableDaysInCurrentMonth()
    {
        return DaySummaries.Where(s => s.HasData).OrderBy(s => s.Day).ToList();
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

    /// <summary>Selects a day tile only when it has data; otherwise clears the current selection.</summary>
    private void SelectDay(CalendarDaySummary? day)
    {
        if (day == null || !day.HasData)
        {
            SelectedDay = null;
            return;
        }

        SelectedDay = day;
    }

    /// <summary>Loads month summaries from persistence and applies them if the view is still on that month.</summary>
    private async Task LoadCurrentMonthAsync()
    {
        IsLoading = true;
        var year = _currentDisplayMonth.Year;
        var month = _currentDisplayMonth.Month;

        IReadOnlyList<CalendarDaySummary> summaries;
        try
        {
            summaries = await Task.Run(() => _dailyStatsService.GetCalendarDaySummaries(year, month));
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to load calendar month {Year}-{Month}", year, month);
            summaries = Array.Empty<CalendarDaySummary>();
        }

        ApplySummaries(summaries, year, month);
        IsLoading = false;
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>Applies month summaries to tile collections, preserves selection, and refreshes today's realtime overlay.</summary>
    private void ApplySummaries(IReadOnlyList<CalendarDaySummary> summaries, int year, int month)
    {
        // Only apply if still on the same month (user may have navigated away).
        if (year != _currentDisplayMonth.Year || month != _currentDisplayMonth.Month)
            return;

        var selectedDay = _selectedDay?.Day;
        DaySummaries.Clear();
        CalendarGridItems.Clear();

        // Leading blank tiles so day 1 aligns with its weekday (Monday = 0).
        var firstDay = new DateTime(year, month, 1);
        var leadingBlanks = ((int)firstDay.DayOfWeek + 6) % 7; // Mon=0 … Sun=6
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
        if (_selectedDay != null && _selectedDay.Day.Year == year && _selectedDay.Day.Month == month)
        {
            var refreshed = summaries.FirstOrDefault(s => s.Day == _selectedDay.Day);
            if (refreshed != null)
                SelectedDay = refreshed;
        }
    }

    /// <summary>Loads per-device detail rows for the selected day and caches today's persisted baseline for live overlays.</summary>
    private async void LoadSelectedDayDetails()
    {
        SelectedDayDetails.Clear();
        _todayPersistedDetailByDevice.Clear();
        if (_selectedDay == null || !_selectedDay.HasData)
            return;

        var day = _selectedDay.Day;
        IReadOnlyList<CalendarDeviceDetail> details;
        try
        {
            details = await Task.Run(() => _dailyStatsService.GetCalendarDayDetail(day));
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to load calendar day detail for {Day}", day);
            details = Array.Empty<CalendarDeviceDetail>();
        }

        if (_selectedDay?.Day != day)
            return;

        SelectedDayDetails.Clear();
        foreach (var d in OrderDetailsForDisplay(details))
            SelectedDayDetails.Add(d);

        var today = DateOnly.FromDateTime(DateTime.Now);
        if (day != today)
            return;

        foreach (var d in details)
            _todayPersistedDetailByDevice[d.DeviceId] = d;

        ApplyRealtimeTodayOverlay();
    }

    /// <summary>Refreshes today's realtime overlay every second while preserving persisted baselines.</summary>
    private void OnSecondTick(object? sender, EventArgs e)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var dayRolledOver = ResetLiveInputOverlayIfDayChanged(today);
        // When the local day rolls over, force one tile-summary refresh to clear stale live overlays.
        RequestOverlayRefresh(updateTileSummary: dayRolledOver);
    }

    /// <summary>Accumulates in-memory input deltas for today and schedules a UI overlay refresh.</summary>
    private void OnInputDeltaIncremented(
        string deviceId,
        (long KeystrokeDelta, long MouseClickDelta, long MouseMovementDelta) delta
    )
    {
        var inputDelta = delta.KeystrokeDelta + delta.MouseClickDelta + delta.MouseMovementDelta;
        if (inputDelta <= 0)
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

            _todayLiveInputDeltaTotal += inputDelta;
        }

        RequestOverlayRefresh();
    }

    /// <summary>Resets in-memory live counters when local time crosses into a new day.</summary>
    private bool ResetLiveInputOverlayIfDayChanged(DateOnly today)
    {
        lock (_liveInputLock)
        {
            if (_accumulatedInputDate == today)
                return false;

            _todayLiveDeltaByDevice.Clear();
            _todayLiveInputDeltaTotal = 0;
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
        var today = DateOnly.FromDateTime(DateTime.Now);

        if (today.Year != _currentDisplayMonth.Year || today.Month != _currentDisplayMonth.Month)
            return;

        var persisted = DaySummaries.FirstOrDefault(d => d.Day == today);
        if (persisted == null)
            return;

        var selectedToday = _selectedDay?.Day == today;
        if (!updateTileSummary && !selectedToday)
            return;

        var nowLocal = DateTime.Now;
        var dayStartLocal = today.ToDateTime(TimeOnly.MinValue);
        Dictionary<string, long>? connectionOverlayByDevice = null;
        if (selectedToday)
        {
            // Connection overlay: open sessions contribute elapsed seconds since max(sessionStart, local midnight).
            connectionOverlayByDevice = new Dictionary<string, long>();
            foreach (var device in _usbMonitorService.DeviceList)
            {
                if (!device.SessionStartedAt.HasValue)
                    continue;

                var startLocal = TimeFormatter.ToLocalTime(device.SessionStartedAt.Value);
                if (startLocal < dayStartLocal)
                    startLocal = dayStartLocal;
                if (startLocal >= nowLocal)
                    continue;

                connectionOverlayByDevice[device.DeviceId] = (long)(nowLocal - startLocal).TotalSeconds;
            }
        }

        if (updateTileSummary)
        {
            var hasConnectionOverlay = selectedToday
                ? connectionOverlayByDevice!.Count > 0
                : _usbMonitorService.DeviceList.Any(device =>
                {
                    if (!device.SessionStartedAt.HasValue)
                        return false;

                    var startLocal = TimeFormatter.ToLocalTime(device.SessionStartedAt.Value);
                    if (startLocal < dayStartLocal)
                        startLocal = dayStartLocal;

                    return startLocal < nowLocal;
                });

            long inputOverlayTotal;
            lock (_liveInputLock)
                inputOverlayTotal = _todayLiveInputDeltaTotal;

            var byId = persisted.Devices.ToDictionary(
                d => d.DeviceId,
                d => new CalendarTileDevice
                {
                    DeviceId = d.DeviceId,
                    DeviceName = d.DeviceName,
                    DeviceType = d.DeviceType,
                    IsConnected = false,
                }
            );
            foreach (var device in _usbMonitorService.DeviceList.Where(d => d.IsConnected))
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
                HasData = persisted.HasData || hasConnectionOverlay || inputOverlayTotal > 0,
                IsSelected = selectedToday,
                Devices = byId
                    .Values.OrderBy(d =>
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
        foreach (var id in ids)
        {
            _todayPersistedDetailByDevice.TryGetValue(id, out var persisted);
            var device = _usbMonitorService.DeviceList.FirstOrDefault(d => d.DeviceId == id);

            var connectionOverlay = connectionOverlayByDevice.TryGetValue(id, out var sec) ? sec : 0L;
            var liveDelta = liveOverlayByDevice.TryGetValue(id, out var overlay)
                ? overlay
                : (KeystrokeDelta: 0L, MouseClickDelta: 0L, MouseMovementDelta: 0L);
            var sessionCount = (persisted?.SessionCount ?? 0) + (device?.IsConnected == true ? 1 : 0);

            var baseConnection = persisted?.ConnectionSeconds ?? 0L;
            var baseLongest = persisted?.LongestSessionSeconds ?? 0L;

            rows.Add(
                new CalendarDeviceDetail
                {
                    DeviceId = id,
                    DeviceName = persisted?.DeviceName ?? device?.DeviceName ?? id,
                    DeviceType = persisted?.DeviceType ?? device?.DeviceType ?? DeviceTypes.Unknown,
                    IsConnected = device?.IsConnected == true,
                    SessionCount = sessionCount,
                    ConnectionSeconds = baseConnection + connectionOverlay,
                    LongestSessionSeconds = Math.Max(baseLongest, connectionOverlay),
                    Keystrokes = persisted?.Keystrokes ?? 0,
                    MouseClicks = persisted?.MouseClicks ?? 0,
                    MouseMovementSeconds = persisted?.MouseMovementSeconds ?? 0,
                    MouseMovementDelta = liveDelta.MouseMovementDelta,
                    KeystrokeDelta = liveDelta.KeystrokeDelta,
                    MouseClickDelta = liveDelta.MouseClickDelta,
                    ActiveMinutes = persisted?.ActiveMinutes ?? 0,
                    HourlyInputCount = persisted?.HourlyInputCount ?? new long[24],
                }
            );
        }

        SelectedDayDetails.Clear();
        foreach (var row in OrderDetailsForDisplay(rows))
            SelectedDayDetails.Add(row);
    }

    /// <summary>Applies consistent display ordering: keyboard, then mouse, then unknown; name as tie-breaker.</summary>
    private static IEnumerable<CalendarDeviceDetail> OrderDetailsForDisplay(IEnumerable<CalendarDeviceDetail> rows)
    {
        return rows.OrderBy(r =>
                r.DeviceType switch
                {
                    DeviceTypes.Keyboard => 0,
                    DeviceTypes.Mouse => 1,
                    _ => 2,
                }
            )
            .ThenBy(r => r.DeviceName);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _appTimerService.SecondTick -= OnSecondTick;
        _rawInputService.InputDeltaIncremented -= OnInputDeltaIncremented;
        _arrowNavigationGate.Dispose();
    }
}
