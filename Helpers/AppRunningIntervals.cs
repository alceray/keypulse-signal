using KeyPulse.Models;

namespace KeyPulse.Helpers;

internal static class AppRunningIntervals
{
    /// <summary>
    /// Reconstructs app-running intervals from AppStarted/AppEnded lifecycle events.
    /// </summary>
    internal static IReadOnlyList<(DateTime Start, DateTime End)> FromOrderedEvents(
        IReadOnlyCollection<DeviceEvent> lifecycleEvents,
        DateTime? end
    )
    {
        var intervals = new List<(DateTime Start, DateTime End)>();
        DateTime? openStart = null;

        // Callers supply only app lifecycle events in chronological order.
        foreach (var appEvent in lifecycleEvents)
        {
            if (appEvent.EventType == EventTypes.AppStarted)
            {
                openStart ??= appEvent.EventTime;
                continue;
            }

            if (openStart.HasValue && appEvent.EventTime.ToUniversalTime() > openStart.Value.ToUniversalTime())
                intervals.Add((openStart.Value, appEvent.EventTime));

            openStart = null;
        }

        if (openStart.HasValue && end.HasValue && end.Value.ToUniversalTime() > openStart.Value.ToUniversalTime())
            intervals.Add((openStart.Value, end.Value));

        return intervals;
    }
}
