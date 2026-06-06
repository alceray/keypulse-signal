using KeyPulse.Models;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace KeyPulse.ViewModels.Dashboard;

/// <summary>
/// Builds the dashboard activity plot for per-device keyboard and mouse input totals.
/// </summary>
internal static class DashboardActivityChartBuilder
{
    /// <summary>Screen-pixel tolerance for selecting / cursor-detecting an activity line (nearest series).</summary>
    public const double LineHitTolerance = 20;

    private const string PlotTitle = "Input Activity";

    // Sub-day tiers label ticks with the time only, except midnight ticks, which also carry the date
    // (see AdaptiveTimeAxis.FormatValueOverride).
    private const string TimeTickFormat = "HH:mm";
    private const string MidnightTickFormat = "MM-dd HH:mm";

    /// <summary>Upper bound on plotted buckets per range, so density stays readable and bounded for performance.</summary>
    private const int MaxBucketCount = 400;

    /// <summary>Human-friendly bucket sizes (minutes), ascending; the finest one under the point cap is chosen.</summary>
    private static readonly int[] BucketSizeLadderMinutes =
    [
        1,
        2,
        5,
        10,
        15,
        30, // sub-hour
        60,
        120,
        180,
        360,
        720, // 1h .. 12h
        1440,
        2880, // 1 day, 2 days
        10080,
        20160, // 1 week, 2 weeks
    ];

    /// <summary>Computed chart inputs. Tick format/spacing is derived from the visible span, not stored here.</summary>
    public sealed record ActivityPlotData(
        string YAxisLabel,
        double? XMinimum,
        double? XMaximum,
        IReadOnlyList<LineSeries> Series
    );

    /// <summary>Builds a standalone activity model (e.g. for tests); live refresh reuses a persistent model.</summary>
    public static PlotModel BuildInputActivityPlot(
        IReadOnlyCollection<ActivitySnapshot> snapshots,
        IReadOnlyCollection<Device> devices,
        IReadOnlyCollection<DeviceEvent> lifecycleEvents,
        DateTime? from,
        DateTime to,
        IReadOnlyDictionary<string, OxyColor> colorsByDevice,
        string? selectedDeviceId = null
    )
    {
        var model = new PlotModel();
        var data = ComputeInputActivityPlot(
            snapshots,
            devices,
            lifecycleEvents,
            from,
            to,
            colorsByDevice,
            selectedDeviceId
        );
        ApplyInputActivityPlot(model, data, resetView: false);
        return model;
    }

    /// <summary>
    /// Builds the activity chart's series and axis config from minute snapshots, with configurable time
    /// resolution and smoothing. Buckets outside app-running intervals are forced to zero. Pure / off-UI-thread.
    /// </summary>
    public static ActivityPlotData ComputeInputActivityPlot(
        IReadOnlyCollection<ActivitySnapshot> snapshots,
        IReadOnlyCollection<Device> devices,
        IReadOnlyCollection<DeviceEvent> lifecycleEvents,
        DateTime? from,
        DateTime to,
        IReadOnlyDictionary<string, OxyColor> colorsByDevice,
        string? selectedDeviceId = null
    )
    {
        var chartStart = ResolveChartStart(snapshots, lifecycleEvents, from);
        var rangeSpan = chartStart.HasValue ? to - chartStart.Value : TimeSpan.Zero;
        if (rangeSpan < TimeSpan.Zero)
            rangeSpan = TimeSpan.Zero;

        var (bucketMinutes, yAxisLabel) = ResolveBucketSize(rangeSpan);

        // Pin the axis to the full requested window so the range shows exactly as selected, regardless of data.
        double? xMinimum = chartStart.HasValue ? DateTimeAxis.ToDouble(chartStart.Value) : null;
        double? xMaximum = chartStart.HasValue ? DateTimeAxis.ToDouble(to) : null;

        var seriesList = new List<LineSeries>();

        var bucketTimeline = BuildBucketTimeline(chartStart, to, bucketMinutes);
        if (bucketTimeline.Count == 0)
            return new ActivityPlotData(yAxisLabel, xMinimum, xMaximum, seriesList);

        var appIntervals = BuildAppRunningIntervals(lifecycleEvents, to);
        var isAppRunningByBucket = bucketTimeline.ToDictionary(
            bucket => bucket,
            bucket => appIntervals.Any(interval => BucketsOverlap(bucket, interval.Start, interval.End, bucketMinutes))
        );

        foreach (var device in devices)
        {
            var deviceSnapshots = snapshots.Where(s => s.DeviceId == device.DeviceId).ToList();
            if (deviceSnapshots.Count == 0)
                continue;

            var valuesByBucket = BuildBucketMetric(
                deviceSnapshots,
                from,
                to,
                GetActivityValueSelector(device.DeviceType),
                isAppRunningByBucket,
                bucketMinutes
            );

            if (valuesByBucket.Values.All(value => value <= 0))
                continue;

            var color = colorsByDevice.TryGetValue(device.DeviceId, out var deviceColor)
                ? deviceColor
                : OxyColors.Automatic;
            if (
                !string.IsNullOrEmpty(selectedDeviceId)
                && !string.Equals(device.DeviceId, selectedDeviceId, StringComparison.OrdinalIgnoreCase)
            )
                color = DashboardDeviceColorPalette.Faded(color);

            var series = new LineSeries
            {
                Title = device.DeviceName,
                StrokeThickness = 2,
                Color = color,
                Tag = device.DeviceId,
            };

            AddPositiveActivityPoints(series, bucketTimeline, valuesByBucket, bucketMinutes);

            seriesList.Add(series);
        }

        return new ActivityPlotData(yAxisLabel, xMinimum, xMaximum, seriesList);
    }

    /// <summary>
    /// Applies computed data to a persistent model in place, reusing its axis objects so pan/zoom survives.
    /// Pass <paramref name="resetView"/> = true on a range switch / first load to refit. UI thread only.
    /// </summary>
    public static void ApplyInputActivityPlot(PlotModel model, ActivityPlotData data, bool resetView)
    {
        ConfigureAxes(model, data);

        model.Series.Clear();
        foreach (var series in data.Series)
            model.Series.Add(series);

        if (resetView)
            model.ResetAllAxes();

        model.InvalidatePlot(true);
    }

    /// <summary>
    /// Builds the activity chart's interaction controller: hover tracker, left-click device selection, and
    /// right-drag horizontal panning with mouse-wheel horizontal zoom (vertical is locked via the value axis).
    /// </summary>
    public static IPlotController BuildActivityChartController(Action<IPlotView, OxyMouseDownEventArgs> onClick)
    {
        var controller = new PlotController();
        controller.UnbindAll();
        controller.Bind(new OxyMouseEnterGesture(), PlotCommands.HoverTrack);
        controller.Bind(
            new OxyMouseDownGesture(OxyMouseButton.Left),
            new DelegatePlotCommand<OxyMouseDownEventArgs>((view, _, args) => onClick(view, args))
        );
        controller.Bind(new OxyMouseDownGesture(OxyMouseButton.Right), PlotCommands.PanAt);
        controller.Bind(new OxyMouseWheelGesture(), PlotCommands.ZoomWheel);
        return controller;
    }

    /// <summary>Creates the time/value axes on first use, then updates their display props in place.</summary>
    private static void ConfigureAxes(PlotModel model, ActivityPlotData data)
    {
        model.Title = PlotTitle;

        if (model.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom) is not AdaptiveTimeAxis timeAxis)
        {
            timeAxis = new AdaptiveTimeAxis { Position = AxisPosition.Bottom, Title = "Time" };
            model.Axes.Add(timeAxis);
            // Re-derive tick format/spacing from the visible span on every zoom/pan/reset.
#pragma warning disable CS0618 // AxisChanged is the supported hook for reacting to pan/zoom in OxyPlot 2.x.
            timeAxis.AxisChanged += (sender, _) =>
            {
                if (((AdaptiveTimeAxis)sender!).ApplyAdaptiveProfile())
                    model.InvalidatePlot(false);
            };
#pragma warning restore CS0618
        }

        timeAxis.Minimum = data.XMinimum ?? double.NaN;
        timeAxis.Maximum = data.XMaximum ?? double.NaN;
        // Confine panning / zooming to the loaded window so the user cannot scroll off into empty time.
        timeAxis.AbsoluteMinimum = data.XMinimum ?? double.MinValue;
        timeAxis.AbsoluteMaximum = data.XMaximum ?? double.MaxValue;
        timeAxis.ApplyAdaptiveProfile();

        if (model.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left) is not LinearAxis valueAxis)
        {
            // Lock the value axis so right-drag pan and wheel zoom only move along the time axis.
            valueAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Minimum = 0,
                IsPanEnabled = false,
                IsZoomEnabled = false,
            };
            model.Axes.Add(valueAxis);
        }

        valueAxis.Title = data.YAxisLabel;
    }

    /// <summary>Time axis whose tick format/spacing tracks the visible span, so labels gain detail on zoom.</summary>
    private sealed class AdaptiveTimeAxis : DateTimeAxis
    {
        /// <summary>Re-derives format/interval/step from the visible span; returns true if anything changed.</summary>
        public bool ApplyAdaptiveProfile()
        {
            // ViewMinimum/Maximum (the zoomed view, set before AxisChanged fires) else the pinned range.
            // ActualMinimum/Maximum are avoided — they default to 0/100 until the first render.
            var min = double.IsNaN(ViewMinimum) ? Minimum : ViewMinimum;
            var max = double.IsNaN(ViewMaximum) ? Maximum : ViewMaximum;
            if (double.IsNaN(min) || double.IsNaN(max) || max <= min)
                return false;

            var (format, interval, majorStep) = ResolveTimeAxisProfile(max - min);
            if (StringFormat == format && IntervalType == interval && majorStep.Equals(MajorStep))
                return false; // Equals handles the NaN == NaN ("auto") case

            StringFormat = format;
            IntervalType = interval;
            MajorStep = majorStep;
            return true;
        }

        /// <summary>In sub-day tiers, midnight ticks carry the date; all other ticks show the time only.</summary>
        protected override string FormatValueOverride(double x)
        {
            if (StringFormat != TimeTickFormat)
                return base.FormatValueOverride(x);

            var time = ConvertToDateTime(x);
            var format = time is { Hour: 0, Minute: 0 } ? MidnightTickFormat : TimeTickFormat;
            return time.ToString(format, ActualCulture);
        }
    }

    /// <summary>
    /// Maps a visible span (in days) to tick format / interval / major step (step in days; NaN = auto).
    /// OxyPlot anchors hour and day ticks to step multiples from midnight, so the steps land on round times.
    /// </summary>
    private static (string Format, DateTimeIntervalType Interval, double MajorStep) ResolveTimeAxisProfile(
        double spanDays
    )
    {
        // Date-only ("MM-dd") tiers use a whole-day step and month-only ("yyyy-MM") steps in whole months, so
        // two ticks never share a label; sub-day steps always pair with a time-bearing format.
        if (spanDays <= 3.0 / 24)
            return (TimeTickFormat, DateTimeIntervalType.Minutes, 15.0 / 1440); // <=3h: every 15 minutes
        if (spanDays <= 0.5)
            return (TimeTickFormat, DateTimeIntervalType.Hours, 1.0 / 24); // <=12h: hourly
        if (spanDays <= 1)
            return (TimeTickFormat, DateTimeIntervalType.Hours, 3.0 / 24); // <=1 day: every 3 hours
        if (spanDays <= 7)
            return ("MM-dd", DateTimeIntervalType.Days, 1); // <=1 week: daily
        if (spanDays <= 31)
            return ("MM-dd", DateTimeIntervalType.Days, 3); // <=1 month: every 3 days
        if (spanDays <= 92)
            return ("MM-dd", DateTimeIntervalType.Days, 7); // <=3 months: weekly
        return ("yyyy-MM", DateTimeIntervalType.Months, double.NaN); // beyond: monthly (auto, whole months)
    }

    /// <summary>
    /// Plots one point per active bucket at the bucket's midpoint, rising from and returning to the baseline at
    /// the run's edges so the straight-segment line stays confined to the run's time span (no overshoot, no leak
    /// into empty buckets). The line breaks across inactive runs, so separate-time usage does not appear to overlap.
    /// </summary>
    private static void AddPositiveActivityPoints(
        LineSeries series,
        IReadOnlyList<DateTime> bucketTimeline,
        IReadOnlyDictionary<DateTime, double> valuesByBucket,
        int bucketMinutes
    )
    {
        var halfBucket = bucketMinutes / 2.0;
        var inRun = false;
        var lastActiveBucket = default(DateTime);
        for (var i = 0; i < bucketTimeline.Count; i++)
        {
            var bucket = bucketTimeline[i];
            valuesByBucket.TryGetValue(bucket, out var value);

            if (value > 0)
            {
                if (!inRun)
                {
                    series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(bucket), 0)); // rise from baseline at run start
                    inRun = true;
                }

                series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(bucket.AddMinutes(halfBucket)), value));
                lastActiveBucket = bucket;
            }
            else if (inRun)
            {
                CloseRunAtBaseline(series, lastActiveBucket, bucketMinutes);
                inRun = false;
            }
        }

        if (inRun)
            CloseRunAtBaseline(series, lastActiveBucket, bucketMinutes);
    }

    /// <summary>Falls back to the baseline at the run's right edge and breaks the line before the next gap.</summary>
    private static void CloseRunAtBaseline(LineSeries series, DateTime lastActiveBucket, int bucketMinutes)
    {
        series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(lastActiveBucket.AddMinutes(bucketMinutes)), 0));
        series.Points.Add(DataPoint.Undefined);
    }

    private static Func<ActivitySnapshot, double> GetActivityValueSelector(DeviceTypes deviceType) =>
        deviceType == DeviceTypes.Keyboard
            ? snapshot => snapshot.Keystrokes
            : snapshot => snapshot.MouseClicks + snapshot.MouseMovementSeconds;

    /// <summary>
    /// Picks the finest ladder bucket that keeps the range under <see cref="MaxBucketCount"/> points, so density
    /// stays roughly constant across ranges. The bucket is the data resolution: zoom magnifies, never refines it.
    /// </summary>
    internal static (int BucketMinutes, string YAxisLabel) ResolveBucketSize(TimeSpan rangeSpan)
    {
        var rangeMinutes = Math.Max(rangeSpan.TotalMinutes, 0);
        var bucketMinutes = BucketSizeLadderMinutes[^1];
        foreach (var candidate in BucketSizeLadderMinutes)
        {
            if (rangeMinutes / candidate <= MaxBucketCount)
            {
                bucketMinutes = candidate;
                break;
            }
        }

        return (bucketMinutes, $"Inputs {FormatBucketLabel(bucketMinutes)}");
    }

    /// <summary>Renders a bucket size as a "per …" label using its largest whole unit (min / hour / day / week).</summary>
    private static string FormatBucketLabel(int bucketMinutes)
    {
        if (bucketMinutes < 60)
            return bucketMinutes == 1 ? "per minute" : $"per {bucketMinutes} min";
        if (bucketMinutes < 1440)
            return PerUnit(bucketMinutes / 60, "hour");
        if (bucketMinutes < 10080)
            return PerUnit(bucketMinutes / 1440, "day");
        return PerUnit(bucketMinutes / 10080, "week");

        static string PerUnit(int count, string unit) => count == 1 ? $"per {unit}" : $"per {count} {unit}s";
    }

    /// <summary>
    /// Resolves the first timestamp that should influence chart density and bucket generation.
    /// </summary>
    private static DateTime? ResolveChartStart(
        IReadOnlyCollection<ActivitySnapshot> snapshots,
        IReadOnlyCollection<DeviceEvent> lifecycleEvents,
        DateTime? from
    )
    {
        if (from.HasValue)
            return from.Value;

        var firstSnapshotMinute = snapshots.Select(s => s.Minute).DefaultIfEmpty().Min();
        var firstLifecycleMinute = lifecycleEvents.Select(e => e.EventTime).DefaultIfEmpty().Min();

        var inferredStart = MinNonDefault(firstSnapshotMinute, firstLifecycleMinute);
        return inferredStart == default ? null : inferredStart;
    }

    /// <summary>
    /// Builds the full bucket timeline that the chart should render for the selected range.
    /// </summary>
    private static List<DateTime> BuildBucketTimeline(DateTime? chartStart, DateTime to, int bucketMinutes)
    {
        if (!chartStart.HasValue)
            return [];

        var start = ToBucketStart(chartStart.Value, bucketMinutes);
        var end = ToBucketStart(to, bucketMinutes);

        if (start == default || end < start)
            return [];

        var buckets = new List<DateTime>();
        for (var bucket = start; bucket <= end; bucket = bucket.AddMinutes(bucketMinutes))
            buckets.Add(bucket);

        return buckets;
    }

    /// <summary>
    /// Aggregates one metric into chart buckets and zeroes values while the app is not running.
    /// </summary>
    private static Dictionary<DateTime, double> BuildBucketMetric(
        IReadOnlyCollection<ActivitySnapshot> snapshots,
        DateTime? from,
        DateTime to,
        Func<ActivitySnapshot, double> selector,
        IReadOnlyDictionary<DateTime, bool> isAppRunningByBucket,
        int bucketMinutes
    )
    {
        var aggregated = snapshots
            .Where(s => (!from.HasValue || s.Minute >= from.Value) && s.Minute <= to)
            .GroupBy(s => ToBucketStart(s.Minute, bucketMinutes))
            .ToDictionary(g => g.Key, g => g.Sum(selector));

        var timeline = isAppRunningByBucket.Keys.OrderBy(t => t).ToList();
        var result = new Dictionary<DateTime, double>(timeline.Count);
        foreach (var bucket in timeline)
        {
            var isAppRunning = isAppRunningByBucket[bucket];
            if (!isAppRunning)
            {
                result[bucket] = 0;
                continue;
            }

            result[bucket] = aggregated.TryGetValue(bucket, out var value) ? value : 0;
        }

        return result;
    }

    /// <summary>
    /// Truncates a timestamp to the configured bucket boundary.
    /// </summary>
    private static DateTime ToBucketStart(DateTime minute, int bucketMinutes)
    {
        var bucketTicks = TimeSpan.FromMinutes(bucketMinutes).Ticks;
        var normalizedTicks = minute.Ticks / bucketTicks * bucketTicks;
        return new DateTime(normalizedTicks, minute.Kind);
    }

    /// <summary>
    /// Reconstructs app-running intervals from AppStarted/AppEnded lifecycle events.
    /// </summary>
    private static IReadOnlyList<(DateTime Start, DateTime End)> BuildAppRunningIntervals(
        IReadOnlyCollection<DeviceEvent> lifecycleEvents,
        DateTime chartEnd
    )
    {
        var intervals = new List<(DateTime Start, DateTime End)>();
        DateTime? openStart = null;

        // lifecycleEvents are expected to be prefiltered and ordered by DataService.GetDashboardEvents.
        foreach (var appEvent in lifecycleEvents)
        {
            if (appEvent.EventType == EventTypes.AppStarted)
            {
                openStart ??= appEvent.EventTime;
                continue;
            }

            if (openStart.HasValue && appEvent.EventTime > openStart.Value)
                intervals.Add((openStart.Value, appEvent.EventTime));

            openStart = null;
        }

        if (openStart.HasValue && chartEnd > openStart.Value)
            intervals.Add((openStart.Value, chartEnd));

        return intervals;
    }

    /// <summary>
    /// Returns true when the chart bucket intersects a running interval.
    /// </summary>
    private static bool BucketsOverlap(
        DateTime bucketStart,
        DateTime intervalStart,
        DateTime intervalEnd,
        int bucketMinutes
    )
    {
        var bucketEnd = bucketStart.AddMinutes(bucketMinutes);
        return bucketStart < intervalEnd && intervalStart < bucketEnd;
    }

    /// <summary>
    /// Returns the earliest non-default timestamp.
    /// </summary>
    private static DateTime MinNonDefault(DateTime a, DateTime b)
    {
        if (a == default)
            return b;
        if (b == default)
            return a;
        return a < b ? a : b;
    }
}
