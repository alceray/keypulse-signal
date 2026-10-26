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

    public PartListViewModel(string kind, CollectionService collection, CatalogService catalogs)
    {
        Kind = kind;
        _collection = collection;
        _catalogs = catalogs;
        Rows = new ListCollectionView(_rows);
        var sections = new PropertyGroupDescription(nameof(PartRow.Section));
        sections.GroupNames.Add("Your collection");
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
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanRemove));
        }
    }
    public bool CanMutate => !_disposed && !_busy && _collection.IsLoaded && _loadError.Length == 0;
    public bool CanEdit => CanMutate && Selected?.Item is { DetailsError: null };
    public bool CanRemove => CanMutate && Selected?.Item != null;

    public bool CanChangeQuantity(PartRow row) =>
        CanMutate
        && !row.IsEditing
        && row.Item?.DetailsError == null
        && (row.Item != null || _collection.GetCatalogStatus(Kind) == CollectionCatalogStatus.Ready);

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

    public async Task<CollectionResult?> SaveQuantityAsync(PartRow row, bool removalConfirmed = false)
    {
        if (!CanChangeQuantity(row) || row.QuantityValue is not int quantity || !row.HasQuantityDraft)
            return null;
        var captured = row.QuantityOriginal;
        if (quantity == 0 && captured != null && !removalConfirmed)
            return null;
        _busy = true;
        Message = "Saving quantity…";
        Notify();
        CollectionResult result;
        if (captured != null)
            result =
                quantity == 0
                    ? await _collection.RemoveAsync(captured)
                    : await _collection.UpdateAsync(captured, quantity, captured.Notes);
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
            row.ResetQuantity();
        Message = result.Succeeded ? $"Saved quantity for {row.Name}." : result.Message ?? "Could not save quantity.";
        Synchronize();
        return result;
    }

    public async Task ReloadAsync()
    {
        // A conflict requires a fresh database read, not a retry of the stale revision.
        var result = await _collection.ReloadAsync();
        if (_disposed)
            return;
        _loadError = result.Succeeded ? "" : result.Message ?? "Could not reload your collection.";
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

    public void Edit(PartRow row)
    {
        if (CanMutate && row.CanEdit)
            row.SetEditor(new(Kind, _collection, row.Item));
    }

    public void CancelEdit(PartRow row)
    {
        if (!CanMutate || row.Editor?.IsBusy == true)
            return;
        row.SetEditor(null);
        if (row.IsNew)
            _rows.Remove(row);
        Rows.Refresh();
        Notify();
    }

    public async Task SaveEditAsync(PartRow row)
    {
        if (!CanMutate || row.Editor is not { CanSave: true } editor)
            return;
        _busy = true;
        Notify();
        var saved = await editor.SaveAsync();
        if (_disposed)
            return;
        _busy = false;
        if (saved)
        {
            row.SetEditor(null);
            if (row.IsNew)
            {
                _rows.Remove(row);
                Rows.Refresh();
            }
        }
        Message = saved ? "Item saved." : editor.Message;
        Synchronize();
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
        OnPropertyChanged(nameof(CanEdit));
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
