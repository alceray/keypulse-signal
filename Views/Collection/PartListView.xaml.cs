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
    private double _verticalOffset,
        _horizontalOffset;

    public PartListView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => ConfigureColumns();
        Loaded += (_, _) =>
        {
            ConfigureColumns();
            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() =>
                {
                    var scroll = FindScrollViewer(PartsGrid);
                    scroll?.ScrollToVerticalOffset(_verticalOffset);
                    scroll?.ScrollToHorizontalOffset(_horizontalOffset);
                })
            );
        };
        Unloaded += (_, _) =>
        {
            var scroll = FindScrollViewer(PartsGrid);
            _verticalOffset = scroll?.VerticalOffset ?? 0;
            _horizontalOffset = scroll?.HorizontalOffset ?? 0;
        };
    }

    private void ConfigureColumns()
    {
        if (Model == null)
            return;
        foreach (var column in new[] { TypeColumn, FamilyColumn, LowProfileColumn })
            column.Visibility = Model.IsSwitch ? Visibility.Visible : Visibility.Collapsed;
        foreach (var column in new[] { ProfileColumn, MaterialColumn })
            column.Visibility = Model.IsSwitch ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Edit(object sender, RoutedEventArgs e)
    {
        if (Model is not { } model || ActionRow(sender) is not { } row)
            return;
        model.Edit(row);
        PartsGrid.ScrollIntoView(row, NotesColumn);
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

    private async void SaveEdit(object sender, RoutedEventArgs e)
    {
        if (Model is { } model && ActionRow(sender) is { } row)
            await model.SaveEditAsync(row);
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
        if (
            Model is not { } model
            || ActionRow(sender) is not { } row
            || !model.CanChangeQuantity(row)
            || row.HasQuantityDraft
        )
            return;
        var quantity = Math.Max(0, row.Quantity + direction * (model.IsSwitch ? 10 : 1));
        if (quantity > PartKinds.MaxQuantity(model.Kind))
        {
            model.ShowMessage($"Maximum quantity is {PartKinds.MaxQuantity(model.Kind):N0}.");
            return;
        }
        row.QuantityText = quantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SaveQuantity(sender, e);
    }

    private async void Remove(object sender, RoutedEventArgs e)
    {
        if (Model is not { CanMutate: true } model || ActionRow(sender) is not { CanRemove: true, Item: { } item })
            return;
        var owner = Window.GetWindow(this);
        if (!ConfirmRemoval(item))
            return;
        var result = await model.RemoveAsync(item);
        if (
            result?.Status == CollectionResultStatus.Conflict
            && IsLoaded
            && MessageBox.Show(
                owner,
                "This item changed. Reload current values? Review them before requesting removal again.",
                "Collection changed",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information
            ) == MessageBoxResult.OK
        )
            await model.ReloadAsync();
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

    private void DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(PartsGrid, e.OriginalSource as DependencyObject) is not DataGridRow)
            return;
        if (
            e.OriginalSource is DependencyObject target
            && (FindAncestor<TextBox>(target) != null || FindAncestor<Button>(target) != null)
        )
            return;
        Edit(sender, e);
    }

    private void GridKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox or Button)
            return;
        if (e.Key == Key.Delete)
        {
            Remove(sender, e);
            e.Handled = true;
        }
        else if (e.Key is Key.Enter or Key.F2)
        {
            Edit(sender, e);
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
            SaveQuantity(sender, e);
            e.Handled = true;
        }
    }

    private async void SaveQuantity(object sender, RoutedEventArgs e)
    {
        if (
            Model is not { } model
            || sender is not FrameworkElement { DataContext: PartRow row }
            || !model.CanChangeQuantity(row)
            || !row.CanSaveQuantity
        )
            return;
        var confirmed = false;
        if (row.QuantityValue == 0 && row.QuantityOriginal is { } item)
        {
            confirmed = ConfirmRemoval(item);
            if (!confirmed)
            {
                row.ResetQuantity();
                return;
            }
        }
        var result = await model.SaveQuantityAsync(row, confirmed);
        if (!IsLoaded)
            return;
        if (result?.Status == CollectionResultStatus.Conflict)
        {
            if (
                MessageBox.Show(
                    Window.GetWindow(this),
                    "This item changed. Reload current values and discard this quantity draft?",
                    "Collection changed",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Information
                ) == MessageBoxResult.OK
            )
            {
                row.ResetQuantity();
                await model.ReloadAsync();
            }
        }
        else if (result?.Succeeded == true && model.Rows.Contains(row))
        {
            PartsGrid.SelectedItem = row;
            PartsGrid.ScrollIntoView(row, QuantityColumn);
            PartsGrid.Focus();
        }
    }

    private bool ConfirmRemoval(CollectionSnapshot item) =>
        MessageBox.Show(
            Window.GetWindow(this),
            $"Remove {item.Name}?\n\nThis removes its quantity ({item.Quantity:N0}) and notes."
                + (item.CatalogEntryId == null ? "\nThis custom item cannot be restored from the catalog." : ""),
            "Remove item",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No
        ) == MessageBoxResult.Yes;

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
