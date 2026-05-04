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
/// Subscribes to AppTimerService.ThirtySecondTick to keep today's tile fresh.
/// </summary>
public sealed class CalendarViewModel : ObservableObject, IDisposable
{
    private readonly DailyStatsService _dailyStatsService;
    private readonly UsbMonitorService _usbMonitorService;
    private readonly RawInputService _rawInputService;
    private readonly AppTimerService _appTimerService;

    // UI-only realtime overlay state for today's row/details.
    private readonly object _liveInputLock = new();
    private readonly Dictionary<string, long> _todayLiveInputDeltaByDevice = [];
    private DateOnly _accumulatedInputDate = DateOnly.FromDateTime(DateTime.Now);
    private readonly Dictionary<string, CalendarDeviceDetail> _todayPersistedDetailByDevice = [];

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

        PreviousMonthCommand = new RelayCommand(_ => NavigateMonth(-1), _ => CanGoPrevious);
        NextMonthCommand = new RelayCommand(_ => NavigateMonth(1), _ => CanGoNext);
        SelectDayCommand = new RelayCommand(day => SelectDay(day as CalendarDaySummary));

        _appTimerService.ThirtySecondTick += OnThirtySecondTick;
        _appTimerService.SecondTick += OnSecondTick;
        _rawInputService.InputCountIncremented += OnInputCountIncremented;
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

        LoadCurrentMonth();
    }

    private void NavigateMonth(int delta)
    {
        _currentDisplayMonth = _currentDisplayMonth
            .AddMonths(delta)
            .AddDays(1 - _currentDisplayMonth.AddMonths(delta).Day);

        OnPropertyChanged(nameof(MonthTitle));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoPrevious));
        SelectedDay = null;
        LoadCurrentMonth();
    }

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
    private async void LoadCurrentMonth()
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

    private void OnSecondTick(object? sender, EventArgs e)
    {
        ApplyRealtimeTodayOverlay();
    }

    private void OnInputCountIncremented(string deviceId, long delta)
    {
        if (delta <= 0)
            return;

        var today = DateOnly.FromDateTime(DateTime.Now);
        lock (_liveInputLock)
        {
            if (_accumulatedInputDate != today)
            {
                _todayLiveInputDeltaByDevice.Clear();
                _accumulatedInputDate = today;
            }

            _todayLiveInputDeltaByDevice[deviceId] =
                (_todayLiveInputDeltaByDevice.TryGetValue(deviceId, out var existing) ? existing : 0L) + delta;
        }

        // Reflect input changes immediately so today's detail panel feels realtime like Device List.
        var dispatcher = Application.Current?.Dispatcher;
        if (!ShutdownDispose.IsDispatcherUsable(dispatcher))
            return;

        if (dispatcher!.CheckAccess())
            ApplyRealtimeTodayOverlay();
        else
            dispatcher.BeginInvoke(new Action(ApplyRealtimeTodayOverlay));
    }

    /// <summary>Refreshes today's persisted baseline every 30 seconds while viewing the current month.</summary>
    private async void OnThirtySecondTick(object? sender, EventArgs e)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (today.Year != _currentDisplayMonth.Year || today.Month != _currentDisplayMonth.Month)
            return;

        try
        {
            var summaries = await Task.Run(() =>
                _dailyStatsService.GetCalendarDaySummaries(_currentDisplayMonth.Year, _currentDisplayMonth.Month)
            );

            // Update today's persisted baseline in DaySummaries
            var todaySummary = summaries.FirstOrDefault(s => s.Day == today);
            if (todaySummary != null)
            {
                for (var i = 0; i < DaySummaries.Count; i++)
                {
                    if (DaySummaries[i].Day == today)
                    {
                        DaySummaries[i] = todaySummary;
                        break;
                    }
                }
            }

            ApplyRealtimeTodayOverlay();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to refresh today's calendar baseline");
        }
    }

    /// <summary>
    /// Applies UI-only realtime overlay for today's tile and selected-today detail panel.
    /// Persistence remains in source-table write paths (DeviceEvents/ActivitySnapshots).
    /// </summary>
    private void ApplyRealtimeTodayOverlay()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        if (today.Year != _currentDisplayMonth.Year || today.Month != _currentDisplayMonth.Month)
            return;

        var persisted = DaySummaries.FirstOrDefault(d => d.Day == today);
        if (persisted == null)
            return;

        var nowLocal = DateTime.Now;
        var dayStartLocal = today.ToDateTime(TimeOnly.MinValue);

        // Connection overlay: open sessions contribute elapsed seconds since max(sessionStart, local midnight).
        var connectionOverlayByDevice = new Dictionary<string, long>();
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

        Dictionary<string, long> inputOverlayByDevice;
        long inputOverlayTotal;
        lock (_liveInputLock)
        {
            inputOverlayByDevice = _todayLiveInputDeltaByDevice.ToDictionary(k => k.Key, v => v.Value);
            inputOverlayTotal = inputOverlayByDevice.Values.Sum();
        }

        var realtimeSummary = new CalendarDaySummary
        {
            Day = persisted.Day,
            HasData = persisted.HasData || connectionOverlayByDevice.Count > 0 || inputOverlayTotal > 0,
            IsSelected = _selectedDay?.Day == today,
            Devices = BuildRealtimeTileDevices(persisted.Devices),
        };

        ReplaceTodaySummary(realtimeSummary);

        if (_selectedDay?.Day == today)
            ApplyRealtimeTodayDetailOverlay(connectionOverlayByDevice, inputOverlayByDevice);
    }

    /// <summary>Builds today's tile device list by merging persisted devices with currently connected devices.</summary>
    private IReadOnlyList<CalendarTileDevice> BuildRealtimeTileDevices(IReadOnlyList<CalendarTileDevice> persisted)
    {
        var byId = persisted.ToDictionary(d => d.DeviceId);
        foreach (var device in _usbMonitorService.DeviceList.Where(d => d.IsConnected))
        {
            if (!byId.ContainsKey(device.DeviceId))
                byId[device.DeviceId] = new CalendarTileDevice
                {
                    DeviceId = device.DeviceId,
                    DeviceName = device.DeviceName,
                    DeviceType = device.DeviceType,
                };
        }

        return byId
            .Values.OrderBy(d =>
                d.DeviceType == DeviceTypes.Keyboard ? 0
                : d.DeviceType == DeviceTypes.Mouse ? 1
                : 2
            )
            .ThenBy(d => d.DeviceName)
            .ToList();
    }

    private void ReplaceTodaySummary(CalendarDaySummary summary)
    {
        for (var i = 0; i < DaySummaries.Count; i++)
        {
            if (DaySummaries[i].Day == summary.Day)
            {
                DaySummaries[i] = summary;
                break;
            }
        }

        for (var i = 0; i < CalendarGridItems.Count; i++)
        {
            if (CalendarGridItems[i] is CalendarDaySummary existing && existing.Day == summary.Day)
            {
                CalendarGridItems[i] = summary;
                break;
            }
        }
    }

    private void ApplySelectionState(DateOnly? selectedDay)
    {
        UpdateSelectionInCollection(DaySummaries, selectedDay);
        UpdateSelectionInGrid(selectedDay);
    }

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
        IReadOnlyDictionary<string, long> inputOverlayByDevice
    )
    {
        if (_selectedDay == null)
            return;

        var ids = new HashSet<string>(_todayPersistedDetailByDevice.Keys);
        foreach (var id in connectionOverlayByDevice.Keys)
            ids.Add(id);
        foreach (var id in inputOverlayByDevice.Keys)
            ids.Add(id);

        var rows = new List<CalendarDeviceDetail>(ids.Count);
        foreach (var id in ids)
        {
            _todayPersistedDetailByDevice.TryGetValue(id, out var persisted);
            var device = _usbMonitorService.DeviceList.FirstOrDefault(d => d.DeviceId == id);

            var connectionOverlay = connectionOverlayByDevice.TryGetValue(id, out var sec) ? sec : 0L;
            var inputOverlay = inputOverlayByDevice.TryGetValue(id, out var input) ? input : 0L;
            var sessionCount = (persisted?.SessionCount ?? 0) + (device?.IsConnected == true ? 1 : 0);

            var baseConnection = persisted?.ConnectionDuration ?? 0L;
            var baseLongest = persisted?.LongestSessionDuration ?? 0L;

            rows.Add(
                new CalendarDeviceDetail
                {
                    DeviceId = id,
                    DeviceName = persisted?.DeviceName ?? device?.DeviceName ?? id,
                    DeviceType = persisted?.DeviceType ?? device?.DeviceType ?? DeviceTypes.Unknown,
                    SessionCount = sessionCount,
                    ConnectionDuration = baseConnection + connectionOverlay,
                    LongestSessionDuration = Math.Max(baseLongest, connectionOverlay),
                    Keystrokes = persisted?.Keystrokes ?? 0,
                    MouseClicks = persisted?.MouseClicks ?? 0,
                    MouseMovementSeconds = persisted?.MouseMovementSeconds ?? 0,
                    LiveInputDelta = inputOverlay,
                    ActiveMinutes = persisted?.ActiveMinutes ?? 0,
                    DistinctActiveHours = persisted?.DistinctActiveHours ?? 0,
                    PeakInputHour = persisted?.PeakInputHour ?? -1,
                }
            );
        }

        SelectedDayDetails.Clear();
        foreach (var row in OrderDetailsForDisplay(rows))
            SelectedDayDetails.Add(row);
    }

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
        _appTimerService.ThirtySecondTick -= OnThirtySecondTick;
        _appTimerService.SecondTick -= OnSecondTick;
        _rawInputService.InputCountIncremented -= OnInputCountIncremented;
    }
}
