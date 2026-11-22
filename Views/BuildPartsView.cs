using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KeyPulse.Models;

namespace KeyPulse.Views;

public sealed class BuildPartsView : StackPanel
{
    public BuildPartsView()
    {
        SetResourceReference(IsDarkThemeProperty, "IsDarkTheme");
        Refresh();
    }

    private static readonly DependencyProperty IsDarkThemeProperty = DependencyProperty.Register(
        "IsDarkTheme", typeof(bool), typeof(BuildPartsView), new PropertyMetadata(false, Refresh));

    public static readonly DependencyProperty BuildProperty = DependencyProperty.Register(
        nameof(Build), typeof(DeviceBuildSnapshot), typeof(BuildPartsView), new PropertyMetadata(null, Refresh));
    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(
        nameof(EmptyText), typeof(string), typeof(BuildPartsView), new PropertyMetadata("", Refresh));
    public static readonly DependencyProperty TextWrappingProperty = DependencyProperty.Register(
        nameof(TextWrapping), typeof(TextWrapping), typeof(BuildPartsView), new PropertyMetadata(TextWrapping.Wrap, Refresh));

    public DeviceBuildSnapshot? Build
    {
        get => (DeviceBuildSnapshot?)GetValue(BuildProperty);
        set => SetValue(BuildProperty, value);
    }

    public string EmptyText
    {
        get => (string)GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    public TextWrapping TextWrapping
    {
        get => (TextWrapping)GetValue(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
    }

    private static void Refresh(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((BuildPartsView)sender).Refresh();

    private void Refresh()
    {
        Children.Clear();
        if (Build == null || Build.Parts.IsEmpty)
        {
            Children.Add(new TextBlock { Text = Build?.Summary ?? EmptyText, TextWrapping = TextWrapping });
            return;
        }

        foreach (var group in Build.Parts.GroupBy(part => part.PartKind).OrderBy(group => group.Key == PartKinds.Switch ? 0 : 1))
        {
            var label = string.Join(", ", group.Select(part => part.Label));
            var row = new DockPanel();
            row.Children.Add(new Image
            {
                Source = (ImageSource)FindResource(
                    (group.Key == PartKinds.Switch ? "SwitchPartIcon" : "KeycapPartIcon")
                    + ((bool)GetValue(IsDarkThemeProperty) ? "Dark" : "Light")),
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Top,
                ToolTip = group.Key == PartKinds.Switch ? "Switches" : "Keycaps",
            });
            row.Children.Add(new TextBlock
            {
                Text = label,
                ToolTip = label,
                TextWrapping = TextWrapping,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            Children.Add(row);
        }
    }
}
