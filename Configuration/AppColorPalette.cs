using System.Windows.Media;

namespace KeyPulse.Configuration;

// App-wide UI colors as frozen WPF named brushes, shared by code and XAML ({x:Static}).
// The OxyPlot chart palette (DashboardDeviceColorPalette) is separate.
public static class AppColorPalette
{
    // Log levels (troubleshooting view)
    public static Brush FatalBrush { get; } = Brushes.Crimson;
    public static Brush ErrorBrush { get; } = Brushes.OrangeRed;
    public static Brush WarningBrush { get; } = Brushes.Goldenrod;
    public static Brush InformationBrush { get; } = Brushes.DarkTurquoise;
    public static Brush MutedBrush { get; } = Brushes.Gray;

    // Text
    public static Brush PrimaryTextBrush { get; } = Brushes.Black;
    public static Brush SecondaryTextBrush { get; } = Brushes.DimGray;

    // Neutral greys: fill, row hover, border/divider, and a stronger popup edge.
    public static Brush SurfaceBrush { get; } = Brushes.WhiteSmoke;
    public static Brush HoverBrush { get; } = Brushes.Gainsboro;
    public static Brush BorderBrush { get; } = Brushes.Gainsboro;

    // Translucent dark toast.
    public static Brush ToastBackgroundBrush { get; } = MakeFrozen(0xCC, 0x1B, 0x1B, 0x1B);

    // Accents and activity metrics.
    public static Brush ConnectedBrush { get; } = Brushes.RoyalBlue;
    public static Brush DisconnectedBrush { get; } = Brushes.Firebrick;
    public static Brush HiddenBrush { get; } = Brushes.DimGray;
    public static Brush ActiveBrush { get; } = Brushes.MediumSeaGreen;

    // Calendar day tiles: data fill, selected-day fill.
    public static Brush CalendarTileBackgroundBrush { get; } = Brushes.OldLace;
    public static Brush CalendarSelectedTileBackgroundBrush { get; } = Brushes.Wheat;

    // Log search highlight
    public static Brush SearchHighlightBrush { get; } = Brushes.Yellow;
    public static Brush SearchHighlightActiveBrush { get; } = Brushes.Orange;

    // Tray pause glyph: a Color (monochrome image), not a Brush.
    public static Color PauseIconColor { get; } = Color.FromRgb(0x33, 0x33, 0x33);

    public static Brush GetLogLevelBrush(string levelName)
    {
        return levelName switch
        {
            "Fatal" => FatalBrush,
            "Error" => ErrorBrush,
            "Warning" => WarningBrush,
            "Information" => InformationBrush,
            "Debug" => MutedBrush,
            _ => Brushes.Transparent,
        };
    }

    public static Brush GetLogTokenBrush(string token)
    {
        return token.ToUpperInvariant() switch
        {
            AppConstants.Troubleshooting.FatalToken => FatalBrush,
            AppConstants.Troubleshooting.ErrorToken => ErrorBrush,
            AppConstants.Troubleshooting.WarningToken => WarningBrush,
            AppConstants.Troubleshooting.InformationToken => InformationBrush,
            AppConstants.Troubleshooting.DebugToken => MutedBrush,
            _ => PrimaryTextBrush,
        };
    }

    private static Brush MakeFrozen(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}
