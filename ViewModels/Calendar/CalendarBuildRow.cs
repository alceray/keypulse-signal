using KeyPulse.Models;

namespace KeyPulse.ViewModels.Calendar;

public sealed record CalendarBuildRow(DeviceBuildDaySnapshot Day, DeviceBuildSnapshot Build, int Position)
{
    public string Label => $"Build {Position}" + (Day.Current?.BuildKey == Build.BuildKey ? " · Current" : "");
    public string Summary => Build.Summary;
}
