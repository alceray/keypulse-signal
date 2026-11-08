using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KeyPulse.Models;
using KeyPulse.ViewModels.Collection;

namespace KeyPulse.Views.Collection;

public partial class PartListView : UserControl
{
    private void ClearSearch(object sender, RoutedEventArgs e)
    {
        if (Model == null)
            return;
        Model.Search = "";
        Model.ApplySearch();
        SearchBox.Focus();
    }

    private PartListViewModel? Model => DataContext as PartListViewModel;
    private double _horizontalOffset,
        _collectionHeight;
    private bool _openingCollection;

    public PartListView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => ConfigureColumns();
        Loaded += (_, _) =>
        {
            ConfigureColumns();
            if (Model is { } model)
            {
                model.IsCollectionExpanded = true;
                model.IsCatalogExpanded = false;
                model.ApplySearch();
                OpenPinnedCollection(this, new RoutedEventArgs());
            }
            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() =>
                {
                    var scroll = FindScrollViewer(PartsGrid);
                    scroll?.ScrollToHorizontalOffset(_horizontalOffset);
                })
            );
        };
        Unloaded += (_, _) =>
        {
            var scroll = FindScrollViewer(PartsGrid);
            _horizontalOffset = scroll?.HorizontalOffset ?? 0;
            Model?.DiscardPendingChanges();
        };
    }

    private void FilterMouseDownOutside(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || sender is not ComboBox current)
            return;
        e.Handled = SwitchFilterAt(current, e.GetPosition(this));
    }

    internal bool SwitchFilterAt(ComboBox current, Point position)
    {
        var target = FindAncestor<ComboBox>(InputHitTest(position) as DependencyObject);
        if (target == null || target == current || target.DataContext is not PartFilter || !target.IsEnabled)
            return false;
        current.IsDropDownOpen = false;
        // Finish the current mouse event and release the old popup's capture before opening another.
        Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() =>
            {
                if (!IsLoaded || !target.IsVisible || !target.IsEnabled)
                    return;
                target.Focus();
                target.IsDropDownOpen = true;
            })
        );
        return true;
    }

    private void ConfigureColumns()
    {
        if (Model == null)
            return;
        foreach (var column in new[] { TypeColumn, FamilyColumn, LowProfileColumn })
            column.Visibility = Model.IsSwitch ? Visibility.Visible : Visibility.Collapsed;
        foreach (var column in new[] { ProfileColumn, MaterialColumn })
            column.Visibility = Model.IsSwitch ? Visibility.Collapsed : Visibility.Visible;
        FillColumnSpace();
    }

    private void FillColumnSpace()
    {
        if (FindScrollViewer(PartsGrid) is not { ViewportWidth: > 0 } scroll)
            return;
        var fixedWidth = PartsGrid
            .Columns.Where(column =>
                column.Visibility == Visibility.Visible && column != NameColumn && column != NotesColumn
            )
            .Sum(column => column.Width.Value);
        var extra = Math.Max(0, scroll.ViewportWidth - fixedWidth - NameColumn.MinWidth - NotesColumn.MinWidth);
        // Explicit pixel widths avoid WPF's grouped-grid star sizing squeezing other columns.
        foreach (var column in new[] { NameColumn, NotesColumn })
        {
            var width = column.MinWidth + extra / 2;
            if (Math.Abs(column.Width.Value - width) > 0.1)
                column.Width = new DataGridLength(width);
        }
    }

    private async void OpenPinnedCollection(object sender, RoutedEventArgs e)
    {
        if (Model?.IsCollectionExpanded != true || _openingCollection)
            return;
        _openingCollection = true;
        try
        {
            await Dispatcher.InvokeAsync(
                () =>
                {
                    if (!IsLoaded || Model?.IsCollectionExpanded != true)
                        return;
                    if (Model?.Rows.Cast<PartRow>().FirstOrDefault(row => row.Item != null || row.IsNew) is { } first)
                        PartsGrid.ScrollIntoView(first);
                    PartsGrid.UpdateLayout();
                    FindScrollViewer(PartsGrid)?.ScrollToTop();
                },
                DispatcherPriority.Loaded
            );
            // Expansion and ScrollToTop queue layout/scroll events; do not treat them as scrolling past the rows.
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        }
        finally
        {
            _openingCollection = false;
        }
    }

    private void TableScrolled(object sender, ScrollChangedEventArgs e)
    {
        var scroll = FindScrollViewer(PartsGrid);
        if (!ReferenceEquals(e.OriginalSource, scroll))
            return;
        if (e.ViewportWidthChange != 0 || e.ExtentWidthChange != 0)
            FillColumnSpace();
        if (
            _openingCollection
            || Model is not { IsCollectionExpanded: true } model
            || model.Rows.Groups is not { Count: > 0 } groups
        )
            return;
        // Virtualization can release the collection group once it leaves the viewport.
        if (PartsGrid.ItemContainerGenerator.ContainerFromItem(groups[0]) is GroupItem { ActualHeight: > 0 } group)
            _collectionHeight = group.ActualHeight;
        if (
            e.VerticalChange <= 0
            || e.ViewportHeightChange != 0
            || _collectionHeight <= 0
            || scroll.VerticalOffset < _collectionHeight
        )
            return;
        model.IsCollectionExpanded = false;
        scroll.ScrollToVerticalOffset(Math.Max(0, scroll.VerticalOffset - _collectionHeight));
    }

    private void AddCustom(object sender, RoutedEventArgs e)
    {
        if (Model is not { CanMutate: true } model)
            return;
        var row = model.AddCustom();
        PartsGrid.ScrollIntoView(row, NameColumn);
    }

    private PartRow? ActionRow(object sender) =>
        (sender as FrameworkElement)?.DataContext as PartRow ?? Model?.Selected;

    private async void SaveRow(object sender, RoutedEventArgs e)
    {
        if (Model is { } model && ActionRow(sender) is { } row)
        {
            if (row.QuantityError.Length > 0)
                model.ShowMessage(row.QuantityError);
            else if (row.QuantityValue == 0 && row.Item != null || row.Editor == null)
                SaveQuantity(sender, e);
            else if (row.Editor.Error.Length > 0)
                model.ShowMessage(row.Editor.Error);
            else
                await model.SaveEditAsync(row);
        }
    }

    private async void NotesLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is Button { Name: "CancelRowButton" or "SaveRowButton" })
            return;
        if (Model is { } model && ActionRow(sender) is { } row && IsLoaded)
        {
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
            if (IsLoaded)
                await model.SaveNotesAsync(row);
        }
    }

    private async void NotesKeyDown(object sender, KeyEventArgs e)
    {
        if (Model is not { } model || ActionRow(sender) is not { } row)
            return;
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await model.SaveNotesAsync(row);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            model.CancelEdit(row);
        }
    }

    private void CancelEdit(object sender, RoutedEventArgs e)
    {
        if (ActionRow(sender) is { } row)
            Model?.CancelEdit(row);
    }

    private void Add(object sender, RoutedEventArgs e) => StepQuantity(sender, e, 1);

    private void Increase(object sender, RoutedEventArgs e) => StepQuantity(sender, e, 1);

    private void Decrease(object sender, RoutedEventArgs e) => StepQuantity(sender, e, -1);

    private void StepQuantity(object sender, RoutedEventArgs e, int direction)
    {
        if (Model is not { } model || ActionRow(sender) is not { } row || !model.CanChangeQuantity(row))
            return;
        if (row.QuantityValue is not int current)
        {
            model.ShowMessage(row.QuantityError);
            return;
        }
        var quantity = Math.Max(0, current + direction * (model.IsSwitch ? 10 : 1));
        if (quantity > PartKinds.MaxQuantity(model.Kind))
        {
            model.ShowMessage($"Maximum quantity is {PartKinds.MaxQuantity(model.Kind):N0}.");
            return;
        }
        row.QuantityText = quantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private void Remove(object sender, RoutedEventArgs e)
    {
        if (Model is not { CanMutate: true } || ActionRow(sender) is not { CanRemove: true } row)
            return;
        row.QuantityText = "0";
    }

    private async void ReloadRow(object sender, RoutedEventArgs e)
    {
        if (Model is not { CanMutate: true, Selected: { } row } model)
            return;
        await model.ReloadAsync();
        model.CancelEdit(row);
    }

    private void CopyName(object sender, RoutedEventArgs e)
    {
        if (Model?.Selected is not { } row)
            return;
        try
        {
            Clipboard.SetText(row.Name);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            Model?.ShowMessage("The clipboard is busy. Try again.");
        }
    }

    private void RightClickRow(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row)
        {
            row.IsSelected = true;
            row.Focus();
        }
    }

    private void GridKeyDown(object sender, KeyEventArgs e)
    {
        if (
            e.OriginalSource is DependencyObject source
            && (
                FindAncestor<TextBox>(source) != null
                || FindAncestor<Button>(source) != null
                || FindAncestor<ComboBox>(source) != null
            )
        )
            return;
        if (e.Key == Key.Delete)
        {
            Remove(sender, e);
            e.Handled = true;
        }
        else if (e.Key is Key.Enter or Key.F2)
        {
            if (Model?.Selected is { } row)
                PartsGrid.ScrollIntoView(row, NotesColumn);
            e.Handled = true;
        }
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            CopyName(sender, e);
            e.Handled = true;
        }
    }

    private void QuantityFocused(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { DataContext: PartRow row } box)
        {
            if (Model != null)
                Model.Selected = row;
            box.SelectAll();
        }
    }

    private void QuantityKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: PartRow row })
            return;
        if (e.Key == Key.Escape)
        {
            row.ResetQuantity();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            SaveRow(sender, e);
            e.Handled = true;
        }
    }

    private async void SaveQuantity(object sender, RoutedEventArgs e)
    {
        if (
            Model is not { } model
            || sender is not FrameworkElement { DataContext: PartRow row }
            || !model.CanMutate
            || !row.CanSaveQuantity
        )
            return;
        var result = await model.SaveQuantityAsync(row);
        if (IsLoaded && result?.Succeeded == true && model.Rows.Contains(row))
        {
            PartsGrid.SelectedItem = row;
            PartsGrid.ScrollIntoView(row, QuantityColumn);
            PartsGrid.Focus();
        }
    }

    private static T? FindAncestor<T>(DependencyObject? element)
        where T : DependencyObject
    {
        while (element != null)
        {
            if (element is T match)
                return match;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private void GridSorting(object sender, DataGridSortingEventArgs e)
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.DataBind,
            new Action(() =>
            {
                if (Model == null)
                    return;
                using (Model.Rows.DeferRefresh())
                {
                    for (var i = Model.Rows.SortDescriptions.Count - 1; i >= 0; i--)
                        if (Model.Rows.SortDescriptions[i].PropertyName == nameof(PartRow.Identity))
                            Model.Rows.SortDescriptions.RemoveAt(i);
                    Model.Rows.SortDescriptions.Add(new(nameof(PartRow.Identity), ListSortDirection.Ascending));
                }
            })
        );
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer viewer)
                return viewer;
            if (FindScrollViewer(child) is { } nested)
                return nested;
        }
        return null;
    }
}
