using System.Text.Json;
using KeyPulse.Configuration;
using KeyPulse.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KeyPulse.Data;

public class ApplicationDbContext : DbContext
{
    public DbSet<Device> Devices { get; set; }
    public DbSet<DeviceEvent> DeviceEvents { get; set; }
    public DbSet<ActivitySnapshot> ActivitySnapshots { get; set; }
    public DbSet<DailyDeviceStat> DailyDeviceStats { get; set; }
    public DbSet<ActivityProjection> ActivityProjections { get; set; }

    private static string GetDatabasePath()
    {
        return AppDataPaths.GetPath(AppConstants.Paths.DatabaseFileName);
    }

    private static DateTime ConvertLocalToUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
            return TruncateToSecond(value);

        if (value.Kind == DateTimeKind.Local)
            return TruncateToSecond(value.ToUniversalTime());

        return TruncateToSecond(DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime());
    }

    private static DateTime ConvertUtcToLocal(DateTime value)
    {
        return TruncateToSecond(DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime());
    }

    private static DateTime TruncateToSecond(DateTime value)
    {
        return new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, value.Kind);
    }

    private static string SerializeHourlyInputCount(long[]? values)
    {
        return JsonSerializer.Serialize(values ?? new long[24], (JsonSerializerOptions?)null);
    }

    private static long[] DeserializeHourlyInputCount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new long[24];

        var parsed = JsonSerializer.Deserialize<long[]>(value, (JsonSerializerOptions?)null) ?? new long[24];
        if (parsed.Length == 24)
            return parsed;

        var normalized = new long[24];
        Array.Copy(parsed, normalized, Math.Min(parsed.Length, normalized.Length));
        return normalized;
    }

    private static bool HourlyInputCountEqual(long[]? left, long[]? right)
    {
        return (left ?? Array.Empty<long>()).SequenceEqual(right ?? Array.Empty<long>());
    }

    private static int HourlyInputCountHash(long[]? values)
    {
        if (values == null)
            return 0;

        var hash = new HashCode();
        foreach (var value in values)
            hash.Add(value);
        return hash.ToHashCode();
    }

    private static long[] SnapshotHourlyInputCount(long[]? values)
    {
        return values == null ? new long[24] : values.ToArray();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseLazyLoadingProxies().UseSqlite($"Data Source={GetDatabasePath()}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var localToUtcConverter = new ValueConverter<DateTime, DateTime>(
            v => ConvertLocalToUtc(v),
            v => ConvertUtcToLocal(v)
        );

        var nullableLocalToUtcConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? ConvertLocalToUtc(v.Value) : null,
            v => v.HasValue ? ConvertUtcToLocal(v.Value) : null
        );

        var hourlyInputCountConverter = new ValueConverter<long[], string>(
            v => SerializeHourlyInputCount(v),
            v => DeserializeHourlyInputCount(v)
        );

        var hourlyInputCountComparer = new ValueComparer<long[]>(
            (left, right) => HourlyInputCountEqual(left, right),
            values => HourlyInputCountHash(values),
            values => SnapshotHourlyInputCount(values)
        );
        modelBuilder
            .Entity<Device>()
            .ToTable("Devices")
            .HasIndex(e => e.DeviceId)
            .HasDatabaseName("Idx_Devices_DeviceId");

        modelBuilder.Entity<Device>().Property(e => e.DeviceType).HasConversion<string>();
        modelBuilder.Entity<Device>().Property(e => e.TotalInputCount).HasDefaultValue(0L);

        modelBuilder.Entity<Device>().Property(e => e.SessionStartedAt).HasConversion(nullableLocalToUtcConverter);
        modelBuilder.Entity<Device>().Property(e => e.LastConnectedAt).HasConversion(nullableLocalToUtcConverter);
        modelBuilder.Entity<Device>().Property(e => e.LastSeenAt).HasConversion(nullableLocalToUtcConverter);
        modelBuilder.Entity<DeviceEvent>().ToTable("DeviceEvents");

        modelBuilder.Entity<DeviceEvent>().Property(e => e.EventType).HasConversion<string>();

        modelBuilder.Entity<DeviceEvent>().Property(e => e.EventTime).HasConversion(localToUtcConverter);
        modelBuilder.Entity<DeviceEvent>().HasIndex(e => e.EventTime).HasDatabaseName("Idx_DeviceEvents_EventTime");

        modelBuilder
            .Entity<DeviceEvent>()
            .HasIndex(e => new { e.DeviceId, e.EventTime })
            .HasDatabaseName("Idx_DeviceEvents_DeviceIdEventTime");

        modelBuilder
            .Entity<DeviceEvent>()
            .HasIndex(e => new
            {
                e.DeviceId,
                e.EventTime,
                e.EventType,
            })
            .IsUnique()
            .HasDatabaseName("Idx_DeviceEvents_Unique");

        modelBuilder.Entity<ActivitySnapshot>().ToTable("ActivitySnapshots");

        modelBuilder.Entity<ActivitySnapshot>().Property(e => e.Minute).HasConversion(localToUtcConverter);
        modelBuilder
            .Entity<ActivitySnapshot>()
            .HasIndex(e => new { e.DeviceId, e.Minute })
            .HasDatabaseName("Idx_ActivitySnapshots_DeviceIdMinute")
            .IsUnique();

        modelBuilder.Entity<ActivitySnapshot>().HasIndex(e => e.Minute).HasDatabaseName("Idx_ActivitySnapshots_Minute");

        modelBuilder.Entity<DailyDeviceStat>().ToTable("DailyDeviceStats");
        modelBuilder.Entity<DailyDeviceStat>().Property(e => e.UpdatedAt).HasConversion(localToUtcConverter);
        modelBuilder
            .Entity<DailyDeviceStat>()
            .Property(e => e.HourlyInputCount)
            .HasConversion(hourlyInputCountConverter)
            .Metadata.SetValueComparer(hourlyInputCountComparer);
        modelBuilder
            .Entity<DailyDeviceStat>()
            .HasIndex(e => new { e.Day, e.DeviceId })
            .HasDatabaseName("Idx_DailyDeviceStats_DayDeviceId")
            .IsUnique();
        modelBuilder.Entity<DailyDeviceStat>().HasIndex(e => e.Day).HasDatabaseName("Idx_DailyDeviceStats_Day");
        modelBuilder
            .Entity<DailyDeviceStat>()
            .HasIndex(e => new { e.DeviceId, e.Day })
            .HasDatabaseName("Idx_DailyDeviceStats_DeviceIdDay");

        modelBuilder.Entity<ActivityProjection>().ToTable("ActivityProjections");
        modelBuilder.Entity<ActivityProjection>().Property(e => e.Minute).HasConversion(localToUtcConverter);
        modelBuilder.Entity<ActivityProjection>().Property(e => e.ProjectedAt).HasConversion(localToUtcConverter);
        modelBuilder
            .Entity<ActivityProjection>()
            .HasIndex(e => new { e.DeviceId, e.Minute })
            .HasDatabaseName("Idx_ActivityProjections_DeviceIdMinute")
            .IsUnique();
    }
}
