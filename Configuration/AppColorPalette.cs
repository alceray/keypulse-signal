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
    public static Brush InformationBrush { get; } = Brushes.CornflowerBlue;
    public static Brush DebugBrush { get; } = Brushes.DimGray;
    public static Brush DividerBrush { get; } = Brushes.Gray;

    // Text
    public static Brush PrimaryTextBrush { get; } = Brushes.Black;
    public static Brush SecondaryTextBrush { get; } = Brushes.DimGray;
    public static Brush PlaceholderTextBrush { get; } = Brushes.Gray;

    // Neutral greys: fill, border/divider, and a stronger popup edge.
    public static Brush SurfaceBrush { get; } = Brushes.WhiteSmoke;
    public static Brush BorderBrush { get; } = Brushes.Gainsboro;
    public static Brush PopupBorderBrush { get; } = Brushes.DarkGray;

    // Translucent dark status toast.
    public static Brush StatusBackgroundBrush { get; } = MakeFrozen(0xCC, 0x1B, 0x1B, 0x1B);

    // Accents and activity metrics.
    public static Brush AccentBrush { get; } = Brushes.RoyalBlue;
    public static Brush ActiveInputBrush { get; } = Brushes.RoyalBlue;
    public static Brush LongestSessionBrush { get; } = Brushes.CornflowerBlue;
    public static Brush HourlyBarBrush { get; } = Brushes.CornflowerBlue;
    public static Brush ActiveTimeBrush { get; } = Brushes.MediumSeaGreen;
    public static Brush MouseMovementBrush { get; } = Brushes.DarkSeaGreen;

    // Calendar day tiles: data fill, selected-day fill.
    public static Brush CalendarTileBackgroundBrush { get; } = Brushes.OldLace;
    public static Brush CalendarSelectedTileBackgroundBrush { get; } = Brushes.Wheat;

    // Device-list status pills (background + foreground per state).
    public static Brush StatusConnectedBackgroundBrush { get; } = Brushes.AliceBlue;
    public static Brush StatusConnectedForegroundBrush { get; } = Brushes.RoyalBlue;
    public static Brush StatusDisconnectedBackgroundBrush { get; } = Brushes.MistyRose;
    public static Brush StatusDisconnectedForegroundBrush { get; } = Brushes.Firebrick;
    public static Brush StatusHiddenBackgroundBrush { get; } = Brushes.LemonChiffon;
    public static Brush StatusHiddenForegroundBrush { get; } = Brushes.DarkGoldenrod;

    // Pie-chart connection status indicator
    public static Brush PieConnectedBrush { get; } = Brushes.CornflowerBlue;
    public static Brush PieDisconnectedBrush { get; } = Brushes.IndianRed;
    public static Brush PieUnknownBrush { get; } = Brushes.Gray;

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
            "Debug" => DebugBrush,
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
            AppConstants.Troubleshooting.DebugToken => DebugBrush,
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
