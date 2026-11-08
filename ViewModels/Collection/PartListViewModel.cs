using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using KeyPulse.Helpers;
using KeyPulse.Models;
using KeyPulse.Services;

namespace KeyPulse.ViewModels.Collection;

public sealed class PartListViewModel : ToastMessageViewModelBase
{
    private readonly CollectionService _collection;
    private readonly CatalogService _catalogs;
    private readonly List<PartRow> _rows = [];
    private readonly DispatcherTimer _searchTimer;
    private Func<string, bool> _searchFilter = _ => true;
    private bool _disposed,
        _busy,
        _loading,
        _synchronizing;
    private string _search = "",
        _loadError = "";
    private PartRow? _selected;
    private int _draftGeneration;

    public PartListViewModel(string kind, CollectionService collection, CatalogService catalogs)
    {
        Kind = kind;
        _collection = collection;
        _catalogs = catalogs;
        Rows = new ListCollectionView(_rows);
        var sections = new PropertyGroupDescription(nameof(PartRow.Section));
        sections.GroupNames.Add("My collection");
        sections.GroupNames.Add("Catalog");
        Rows.GroupDescriptions.Add(sections);
        Rows.SortDescriptions.Add(new(nameof(PartRow.Name), ListSortDirection.Ascending));
        Rows.SortDescriptions.Add(new(nameof(PartRow.Identity), ListSortDirection.Ascending));
        Filters = IsSwitch
            ?
            [
                new("Type", x => x.Type),
                new("Family", x => x.Family),
                new("Low profile", x => x.LowProfile),
                new("Manufacturer", x => x.Manufacturer),
                new("Brand", x => x.Brand),
            ]
            :
            [
                new("Profile", x => x.Profile),
                new("Material", x => x.Material),
                new("Manufacturer", x => x.Manufacturer),
            ];
        foreach (var filter in Filters)
            filter.PropertyChanged += FilterChanged;
        Rows.Filter = o =>
            o is PartRow row && (row.IsNew || (_searchFilter(row.SearchText) && Filters.All(f => f.Matches(row))));
        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _searchTimer.Tick += SearchTick;
        RetryCommand = new AsyncRelayCommand(_ => InitializeAsync(true), _ => !_busy && !_loading);
        ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
        _collection.Changed += CollectionChanged;
        Synchronize();
    }

    public string Kind { get; }
    internal CollectionService Collection => _collection;
    public bool IsSwitch => Kind == PartKinds.Switch;
    public string Category => IsSwitch ? "Switches" : "Keycaps";
    private bool _isCollectionExpanded = true,
        _isCatalogExpanded = true;
    public bool IsCollectionExpanded
    {
        get => _isCollectionExpanded;
        set
        {
            _isCollectionExpanded = value;
            OnPropertyChanged();
        }
    }
    public bool IsCatalogExpanded
    {
        get => _isCatalogExpanded;
        set
        {
            _isCatalogExpanded = value;
            OnPropertyChanged();
        }
    }
    public ListCollectionView Rows { get; }
    public IReadOnlyList<PartFilter> Filters { get; }
    public AsyncRelayCommand RetryCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }
    public string Search
    {
        get => _search;
        set
        {
            _search = value;
            OnPropertyChanged();
            _searchTimer.Stop();
            _searchTimer.Start();
        }
    }
    public PartRow? Selected
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value))
                return;
            _selected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanRemove));
        }
    }
    public bool CanMutate => !_disposed && !_busy && _collection.IsLoaded && _loadError.Length == 0;
    public bool CanRemove => CanMutate && Selected?.CanRemove == true;

    public bool CanChangeQuantity(PartRow row) =>
        CanMutate
        && row.Editor?.HasConflict != true
        && row.Item?.DetailsError == null
        && (row.IsNew || row.Item != null || _collection.GetCatalogStatus(Kind) == CollectionCatalogStatus.Ready);

    public string Message
    {
        get => ToastMessage;
        private set
        {
            ToastMessage = value;
            OnPropertyChanged();
        }
    }
    public string CatalogMessage =>
        _collection.GetCatalogStatus(Kind) switch
        {
            CollectionCatalogStatus.Loading => "Loading catalog…",
            CollectionCatalogStatus.CatalogUnavailable =>
                "Catalog unavailable. Your collection and custom items are still available.",
            CollectionCatalogStatus.RefreshFailed =>
                "Catalog refresh failed. Retry to refresh descriptions and add catalog items.",
            _ => "",
        };
    public bool ShowRetry =>
        _loadError.Length > 0
        || _collection.GetCatalogStatus(Kind)
            is CollectionCatalogStatus.CatalogUnavailable
                or CollectionCatalogStatus.RefreshFailed;
    public string EmptyText =>
        _loadError.Length > 0 ? _loadError
        : !_collection.IsLoaded ? "Loading your collection…"
        : Rows.Count > 0 ? ""
        : _rows.Count > 0 ? "No matching items. Change or clear your filters."
        : "No items available. Create a custom item or retry the catalog.";

    public async Task InitializeAsync(bool retry = false)
    {
        if (_disposed || _loading)
            return;
        _loading = true;
        Notify();
        var result = await _collection.LoadAsync(retry);
        if (_disposed)
            return;
        _loadError = result.Succeeded ? "" : result.Message ?? "Could not load your collection. Retry.";
        Synchronize();
        if (result.Succeeded)
            await _collection.RefreshAsync(Kind, retry);
        if (_disposed)
            return;
        _loading = false;
        Synchronize();
    }

    public void ApplySearch()
    {
        _searchTimer.Stop();
        _searchFilter = _catalogs.Search.CreateFilter(Search);
        Rows.Refresh();
        Notify();
    }

    public void ClearFilters()
    {
        Search = "";
        _synchronizing = true;
        foreach (var filter in Filters)
            filter.Selected = PartFilter.Any;
        _synchronizing = false;
        ApplySearch();
    }

    public async Task<CollectionResult?> RemoveAsync(CollectionSnapshot captured)
    {
        if (!CanMutate)
            return null;
        _busy = true;
        Notify();
        var result = await _collection.RemoveAsync(captured);
        if (!_disposed)
        {
            _busy = false;
            Message = result.Succeeded ? $"Removed {captured.Name}." : result.Message ?? "Could not remove item.";
            Synchronize();
        }
        return result;
    }

    public async Task<CollectionResult?> SaveQuantityAsync(PartRow row)
    {
        if (!CanMutate || !row.CanSaveQuantity || row.QuantityValue is not int quantity)
            return null;
        if (!row.IsPendingRemoval && !CanChangeQuantity(row))
            return null;
        var captured = row.QuantityOriginal;
        var draftGeneration = _draftGeneration;
        _busy = true;
        Message = "Saving quantity…";
        Notify();
        CollectionResult result;
        if (captured != null)
        {
            if (quantity == 0)
                result = await _collection.RemoveAsync(captured);
            else if (row.Editor is { } editor)
            {
                if (!editor.CanSave)
                {
                    _busy = false;
                    Notify();
                    return null;
                }
                await editor.SaveAsync();
                result = editor.LastResult!;
            }
            else
                result = await _collection.UpdateAsync(captured, quantity, captured.Notes);
        }
        else if (_collection.FindOwned(Kind, row.Entry!.Id) != null)
            result = CollectionResult.Failure(
                CollectionResultStatus.Conflict,
                "This item changed. Reload before saving."
            );
        else if (quantity == 0)
            result = new(CollectionResultStatus.Success, _collection.Items);
        else
            result = await _collection.AddCatalogAsync(Kind, row.Entry!.Id, quantity);
        if (_disposed)
            return result;
        _busy = false;
        if (result.Succeeded)
        {
            if (quantity == 0)
                row.SetEditor(null);
            row.ResetQuantity();
        }
        Message = result.Succeeded
            ? quantity == 0 && captured != null
                ? $"Removed {row.Name}."
                : $"Saved quantity for {row.Name}."
            : result.Message ?? "Could not save quantity.";
        Synchronize();
        DiscardIfTabChanged(row, draftGeneration);
        return result;
    }

    public async Task ReloadAsync()
    {
        // A conflict requires a fresh database read, not a retry of the stale revision.
        var result = await _collection.ReloadAsync();
        if (_disposed)
            return;
        _loadError = result.Succeeded ? "" : result.Message ?? "Could not reload your collection.";
        if (result.Succeeded)
            foreach (var row in _rows.Where(x => x.Editor is { HasConflict: true, HasChanges: false }))
                if (result.Items.FirstOrDefault(x => x.ItemKey == row.Editor!.OriginalItem?.ItemKey) is { } current)
                    row.Editor!.Reset(current);
        Synchronize();
    }

    public PartRow? AddCustom()
    {
        if (!CanMutate)
            return null;
        IsCollectionExpanded = true;
        var row = _rows.FirstOrDefault(x => x.IsNew);
        if (row == null)
        {
            row = new PartRow(Kind, "draft:" + Guid.NewGuid());
            row.SetEditor(new(Kind, _collection));
            _rows.Insert(0, row);
            Rows.Refresh();
        }
        Selected = row;
        return row;
    }

    public void CancelEdit(PartRow row)
    {
        if (!CanMutate || row.Editor?.IsBusy == true)
            return;
        DiscardDraft(row);
        Rows.Refresh();
        Notify();
    }

    public void DiscardPendingChanges()
    {
        if (_disposed)
            return;
        _draftGeneration++;
        var pending = _rows.Where(row => row.IsEditing && row.Editor?.IsBusy != true).ToArray();
        if (pending.Length == 0)
            return;
        foreach (var row in pending)
            DiscardDraft(row);
        Rows.Refresh();
        Notify();
    }

    private void DiscardDraft(PartRow row)
    {
        if (row.IsNew)
        {
            row.SetEditor(null);
            _rows.Remove(row);
        }
        else
        {
            row.SetEditor(row.Item is { DetailsError: null } item ? new(Kind, _collection, item) : null);
            row.ResetQuantity();
        }
    }

    private void DiscardIfTabChanged(PartRow row, int generation)
    {
        if (generation == _draftGeneration || !_rows.Contains(row))
            return;
        DiscardDraft(row);
        Rows.Refresh();
        Notify();
    }

    public async Task SaveEditAsync(PartRow row)
    {
        if (!CanMutate || row.Editor is not { CanSave: true } editor)
            return;
        var draftGeneration = _draftGeneration;
        _busy = true;
        Notify();
        var saved = await editor.SaveAsync();
        if (_disposed)
            return;
        _busy = false;
        if (saved)
        {
            if (row.IsNew)
            {
                row.SetEditor(null);
                _rows.Remove(row);
                Rows.Refresh();
            }
        }
        Message = saved ? "Item saved." : editor.Message;
        Synchronize();
        DiscardIfTabChanged(row, draftGeneration);
    }

    public async Task SaveNotesAsync(PartRow row)
    {
        if (_disposed || !_collection.IsLoaded || _loadError.Length > 0 || row.Editor == null)
            return;
        if (row.Editor[nameof(PartEditorViewModel.Notes)] is { Length: > 0 } error)
        {
            Message = error;
            return;
        }
        var draftGeneration = _draftGeneration;
        var result = await row.Editor.SaveNotesAsync();
        if (_disposed || result == null)
            return;
        Message = result.Succeeded ? "Notes saved." : result.Message ?? "Could not save notes.";
        Synchronize();
        DiscardIfTabChanged(row, draftGeneration);
    }

    public void ShowMessage(string message) => Message = message;

    private void Synchronize()
    {
        if (_disposed)
            return;
        var selectedId = Selected?.Identity;
        var owned = _collection.Items.Where(x => x.PartKind == Kind).ToArray();
        var existing = _rows.ToDictionary(x => x.Identity);
        var next = _rows.Where(x => x.IsNew).ToList();
        var state = _catalogs.GetState(Kind);
        var ownedIds = new HashSet<string>();
        var changed = false;
        foreach (var item in owned)
        {
            var identity = item.CatalogEntryId == null ? "custom:" + item.ItemKey : "catalog:" + item.CatalogEntryId;
            var row = existing.GetValueOrDefault(identity) ?? new PartRow(Kind, identity);
            state.ById.TryGetValue(item.CatalogEntryId ?? "", out var entry);
            changed |= row.Update(
                item,
                entry,
                _collection.GetItemStatus(item),
                _catalogs.Search,
                _collection.GetCatalogStatus(Kind) == CollectionCatalogStatus.Ready
            );
            if (item.DetailsError == null)
            {
                if (row.Editor == null && !row.HasQuantityDraft)
                    row.SetEditor(new(Kind, _collection, item));
                else
                    row.Editor?.Refresh(item);
            }
            else if (row.Editor != null)
                row.SetEditor(null);
            next.Add(row);
            if (item.CatalogEntryId != null)
                ownedIds.Add(item.CatalogEntryId);
        }
        foreach (var entry in state.Entries.Where(x => !ownedIds.Contains(x.Id)))
        {
            var identity = "catalog:" + entry.Id;
            var row = existing.GetValueOrDefault(identity) ?? new PartRow(Kind, identity);
            changed |= row.Update(
                null,
                entry,
                "",
                _catalogs.Search,
                _collection.GetCatalogStatus(Kind) == CollectionCatalogStatus.Ready,
                state.SearchText[entry.Id]
            );
            if (row.Editor is { HasChanges: false, HasConflict: false, IsBusy: false })
                row.SetEditor(null);
            next.Add(row);
        }
        if (!changed && _rows.SequenceEqual(next))
        {
            Notify();
            return;
        }
        _synchronizing = true;
        using (Rows.DeferRefresh())
        {
            _rows.Clear();
            _rows.AddRange(next);
            foreach (var filter in Filters)
                filter.UpdateOptions(_rows);
        }
        _synchronizing = false;
        Rows.Refresh();
        _selected = _rows.FirstOrDefault(x => x.Identity == selectedId);
        Notify();
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(Selected));
        OnPropertyChanged(nameof(CanMutate));
        OnPropertyChanged(nameof(CanRemove));
        OnPropertyChanged(nameof(CatalogMessage));
        OnPropertyChanged(nameof(ShowRetry));
        OnPropertyChanged(nameof(EmptyText));
        AsyncRelayCommand.RaiseCanExecuteChanged();
    }

    private void CollectionChanged(string kind)
    {
        if (kind == Kind)
            Synchronize();
    }

    private void SearchTick(object? sender, EventArgs args) => ApplySearch();

    private void FilterChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!_synchronizing && args.PropertyName == nameof(PartFilter.Selected))
        {
            Rows.Refresh();
            Notify();
        }
    }

    public override void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (var row in _rows)
            row.SetEditor(null);
        base.Dispose();
        _searchTimer.Stop();
        _searchTimer.Tick -= SearchTick;
        _collection.Changed -= CollectionChanged;
        foreach (var filter in Filters)
            filter.PropertyChanged -= FilterChanged;
    }
}
