using KeyPulse.Configuration;
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
    private const int SMOOTHING_WINDOW = AppConstants.Dashboard.DefaultSmoothingWindow;

    /// <summary>Screen-pixel tolerance for selecting / cursor-detecting an activity line (nearest series).</summary>
    public const double LineHitTolerance = 20;

    private const string PlotTitle = "Input Activity";

    /// <summary>Computed chart inputs: axis config, full-range X bounds, and the built series.</summary>
    public sealed record ActivityPlotData(
        string YAxisLabel,
        string XStringFormat,
        DateTimeIntervalType XIntervalType,
        double XMajorStep,
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

        // Dynamic aggregation, label, and tick spacing. xMajorStep is in days (NaN = auto); OxyPlot anchors
        // hour/day ticks to step multiples from midnight, so a 3h / 1d step lands on round clock times.
        int bucketMinutes;
        string yAxisLabel;
        var xMajorStep = double.NaN;
        if (rangeSpan.TotalDays <= 1)
        {
            bucketMinutes = 10;
            yAxisLabel = "Input count per 10 min";
            xMajorStep = 3.0 / 24; // one tick every 3 hours
        }
        else if (rangeSpan.TotalDays <= 7)
        {
            bucketMinutes = 60;
            yAxisLabel = "Input count per hour";
            xMajorStep = 1; // one tick per day
        }
        else if (rangeSpan.TotalDays <= 93) // 3 months
        {
            bucketMinutes = 360;
            yAxisLabel = "Input count per 6 hours";
        }
        else if (rangeSpan.TotalDays <= 370)
        {
            bucketMinutes = 1440;
            yAxisLabel = "Input count per day";
        }
        else
        {
            bucketMinutes = 10080;
            yAxisLabel = "Input count per week";
        }

        var monthsOnly = rangeSpan.TotalDays >= 365;
        var datesOnly = !monthsOnly && rangeSpan.TotalDays >= 7;

        var xStringFormat =
            monthsOnly ? "yyyy-MM"
            : datesOnly ? "MM-dd"
            : "MM-dd HH:mm";
        var xIntervalType =
            monthsOnly ? DateTimeIntervalType.Months
            : datesOnly ? DateTimeIntervalType.Days
            : DateTimeIntervalType.Hours;

        // Pin the axis to the full requested window so the range shows exactly as selected, regardless of data.
        double? xMinimum = chartStart.HasValue ? DateTimeAxis.ToDouble(chartStart.Value) : null;
        double? xMaximum = chartStart.HasValue ? DateTimeAxis.ToDouble(to) : null;

        var seriesList = new List<LineSeries>();

        var bucketTimeline = BuildBucketTimeline(chartStart, to, bucketMinutes);
        if (bucketTimeline.Count == 0)
            return new ActivityPlotData(
                yAxisLabel,
                xStringFormat,
                xIntervalType,
                xMajorStep,
                xMinimum,
                xMaximum,
                seriesList
            );

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

            valuesByBucket = SmoothBuckets(valuesByBucket, bucketTimeline, isAppRunningByBucket, SMOOTHING_WINDOW);
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

            AddPositiveActivityPoints(series, bucketTimeline, valuesByBucket);

            seriesList.Add(series);
        }

        return new ActivityPlotData(
            yAxisLabel,
            xStringFormat,
            xIntervalType,
            xMajorStep,
            xMinimum,
            xMaximum,
            seriesList
        );
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

    /// <summary>Creates the time/value axes on first use, then updates their display props in place.</summary>
    private static void ConfigureAxes(PlotModel model, ActivityPlotData data)
    {
        model.Title = PlotTitle;

        if (model.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom) is not DateTimeAxis timeAxis)
        {
            timeAxis = new DateTimeAxis { Position = AxisPosition.Bottom, Title = "Time" };
            model.Axes.Add(timeAxis);
        }

        timeAxis.StringFormat = data.XStringFormat;
        timeAxis.IntervalType = data.XIntervalType;
        timeAxis.MajorStep = data.XMajorStep;
        timeAxis.Minimum = data.XMinimum ?? double.NaN;
        timeAxis.Maximum = data.XMaximum ?? double.NaN;

        if (model.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left) is not LinearAxis valueAxis)
        {
            valueAxis = new LinearAxis { Position = AxisPosition.Left, Minimum = 0 };
            model.Axes.Add(valueAxis);
        }

        valueAxis.Title = data.YAxisLabel;
    }

    private static void AddPositiveActivityPoints(
        LineSeries series,
        IReadOnlyList<DateTime> bucketTimeline,
        IReadOnlyDictionary<DateTime, double> valuesByBucket
    )
    {
        var hasOpenSegment = false;
        for (var i = 0; i < bucketTimeline.Count; i++)
        {
            var bucket = bucketTimeline[i];
            valuesByBucket.TryGetValue(bucket, out var value);
            var shouldPlot = value > 0 || HasPositiveNeighbor(bucketTimeline, valuesByBucket, i);
            if (!shouldPlot)
            {
                if (hasOpenSegment)
                {
                    series.Points.Add(DataPoint.Undefined);
                    hasOpenSegment = false;
                }

                continue;
            }

            series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(bucket), value));
            hasOpenSegment = true;
        }
    }

    private static bool HasPositiveNeighbor(
        IReadOnlyList<DateTime> bucketTimeline,
        IReadOnlyDictionary<DateTime, double> valuesByBucket,
        int index
    )
    {
        if (index > 0 && valuesByBucket.TryGetValue(bucketTimeline[index - 1], out var previous) && previous > 0)
            return true;

        return index + 1 < bucketTimeline.Count
            && valuesByBucket.TryGetValue(bucketTimeline[index + 1], out var next)
            && next > 0;
    }

    private static Func<ActivitySnapshot, double> GetActivityValueSelector(DeviceTypes deviceType) =>
        deviceType == DeviceTypes.Keyboard
            ? snapshot => snapshot.Keystrokes
            : snapshot => snapshot.MouseClicks + snapshot.MouseMovementSeconds;

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
    /// Applies trailing moving-average smoothing across consecutive running buckets.
    /// Smoothing never crosses app-off boundaries.
    /// </summary>
    private static Dictionary<DateTime, double> SmoothBuckets(
        IReadOnlyDictionary<DateTime, double> valuesByBucket,
        IReadOnlyList<DateTime> timeline,
        IReadOnlyDictionary<DateTime, bool> isAppRunningByBucket,
        int smoothingWindow
    )
    {
        if (smoothingWindow <= 1)
            return timeline.ToDictionary(
                bucket => bucket,
                bucket => valuesByBucket.TryGetValue(bucket, out var value) ? value : 0
            );

        var result = new Dictionary<DateTime, double>(timeline.Count);
        for (var i = 0; i < timeline.Count; i++)
        {
            var bucket = timeline[i];
            if (!isAppRunningByBucket[bucket])
            {
                result[bucket] = 0;
                continue;
            }

            var sum = 0.0;
            var count = 0;
            for (var j = i; j >= 0 && count < smoothingWindow; j--)
            {
                var candidate = timeline[j];
                if (!isAppRunningByBucket[candidate])
                    break;

                sum += valuesByBucket.TryGetValue(candidate, out var value) ? value : 0;
                count++;
            }

            result[bucket] = count > 0 ? sum / count : 0;
        }

        return result;
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
