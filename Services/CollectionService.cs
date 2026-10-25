using System.Collections.Immutable;
using System.Windows;
using KeyPulse.Helpers;
using KeyPulse.Models;

namespace KeyPulse.Services;

public enum CollectionCatalogStatus
{
    Loading,
    Ready,
    CatalogUnavailable,
    RefreshFailed,
}

public sealed class CollectionService
{
    private readonly DataService _data;
    private readonly CatalogService _catalogs;
    private readonly Action<Action> _dispatch;
    private readonly object _sync = new();
    private readonly Dictionary<string, CollectionCatalogStatus> _states = [];
    private readonly Dictionary<string, Task> _refreshes = [];
    private readonly List<Task> _pending = [];
    private Task<CollectionResult>? _load;
    private bool _accepting = true;

    public CollectionService(DataService data, CatalogService catalogs)
        : this(data, catalogs, NotifyOnDispatcher) { }

    internal CollectionService(DataService data, CatalogService catalogs, Action<Action> dispatch)
    {
        _data = data;
        _catalogs = catalogs;
        _dispatch = dispatch;
    }

    public event Action<string>? Changed;
    public bool IsLoaded => _data.CollectionLoaded;
    public ImmutableArray<CollectionSnapshot> Items => _data.CollectionItems;

    public CollectionCatalogStatus GetCatalogStatus(string kind)
    {
        _ = PartKinds.Unit(kind);
        lock (_sync)
            return _states.GetValueOrDefault(kind, CollectionCatalogStatus.Loading);
    }

    public CollectionSnapshot? FindOwned(string kind, string id) =>
        Items.FirstOrDefault(x => x.PartKind == kind && x.CatalogEntryId == id);

    public string GetItemStatus(CollectionSnapshot row)
    {
        if (row.DetailsError != null)
            return "Details unavailable";
        if (row.CatalogEntryId == null)
            return "Custom";
        return GetCatalogStatus(row.PartKind) switch
        {
            CollectionCatalogStatus.Loading => "Loading catalog",
            CollectionCatalogStatus.CatalogUnavailable => "Catalog unavailable",
            CollectionCatalogStatus.RefreshFailed => "Catalog refresh failed",
            _ => _catalogs.GetState(row.PartKind).ById.ContainsKey(row.CatalogEntryId) ? "" : "Not in catalog",
        };
    }

    public Task<CollectionResult> LoadAsync(bool retry = false)
    {
        lock (_sync)
        {
            if (_load != null && !(retry && _load.IsCompletedSuccessfully && !_load.Result.Succeeded))
                return _load;
            if (!_accepting)
                return Task.FromResult(Closing());
            _load = Task.Run(() =>
            {
                var result = _data.ReadCollectionItems();
                Notify(PartKinds.Switch);
                Notify(PartKinds.Keycap);
                return result;
            });
            return _load;
        }
    }

    // Called by the tab on first access; owned rows publish before catalog work finishes.
    public async Task<CollectionResult> InitializeAsync(bool retry = false)
    {
        var owned = await LoadAsync(retry).ConfigureAwait(false);
        if (!owned.Succeeded)
            return owned;
        await Task.WhenAll(RefreshAsync(PartKinds.Switch, retry), RefreshAsync(PartKinds.Keycap, retry))
            .ConfigureAwait(false);
        return new(CollectionResultStatus.Success, Items);
    }

    public Task RefreshAsync(string kind, bool retry = false)
    {
        _ = PartKinds.Unit(kind);
        lock (_sync)
        {
            if (_refreshes.TryGetValue(kind, out var existing) && (!existing.IsCompleted || !retry))
                return existing;
            if (!_accepting)
                return Task.CompletedTask;
            _states[kind] = CollectionCatalogStatus.Loading;
            var work = Task.Run(async () =>
            {
                var owned = await LoadAsync(retry).ConfigureAwait(false);
                var catalog = await _catalogs.LoadAsync(kind, retry).ConfigureAwait(false);
                var state = CollectionCatalogStatus.CatalogUnavailable;
                if (catalog.Status == CatalogLoadStatus.Loaded)
                    state =
                        owned.Succeeded && _data.RefreshCollectionSnapshots(kind).Succeeded
                            ? CollectionCatalogStatus.Ready
                            : CollectionCatalogStatus.RefreshFailed;
                lock (_sync)
                    _states[kind] = state;
                Notify(kind);
            });
            _refreshes[kind] = work;
            return work;
        }
    }

    public Task<CollectionResult> AddCatalogAsync(string kind, string id, int quantity) =>
        WriteAsync(
            kind,
            () =>
                GetCatalogStatus(kind) == CollectionCatalogStatus.Ready
                    ? _data.AddCatalogCollectionItem(kind, id, quantity)
                    : CollectionResult.Failure(
                        CollectionResultStatus.ValidationError,
                        "Wait for the catalog to finish loading or retry its refresh."
                    )
        );

    public Task<CollectionResult> AddCustomAsync(
        string kind,
        PartDescription description,
        int quantity,
        string? notes
    ) => WriteAsync(kind, () => _data.AddCustomCollectionItem(kind, description, quantity, notes));

    public Task<CollectionResult> UpdateAsync(
        CollectionSnapshot item,
        int quantity,
        string? notes,
        PartDescription? description = null
    ) =>
        WriteAsync(
            item.PartKind,
            () => _data.UpdateCollectionItem(item.ItemKey, item.Revision, quantity, notes, description)
        );

    public Task<CollectionResult> RemoveAsync(CollectionSnapshot item) =>
        WriteAsync(item.PartKind, () => _data.RemoveCollectionItem(item.ItemKey, item.Revision));

    private Task<CollectionResult> WriteAsync(string kind, Func<CollectionResult> write)
    {
        lock (_sync)
        {
            if (!_accepting)
                return Task.FromResult(Closing());
            _pending.RemoveAll(x => x.IsCompleted);
            var task = Task.Run(() =>
            {
                var result = write();
                if (result.Succeeded)
                    Notify(kind);
                return result;
            });
            _pending.Add(task);
            return task;
        }
    }

    public Task StopAndDrainAsync()
    {
        lock (_sync)
        {
            _accepting = false;
            return Task.WhenAll(_pending.Concat(_refreshes.Values).Concat(_load == null ? [] : new Task[] { _load }));
        }
    }

    private static CollectionResult Closing() =>
        CollectionResult.Failure(CollectionResultStatus.ValidationError, "The collection is closing.");

    private void Notify(string kind) => _dispatch(() => Changed?.Invoke(kind));

    private static void NotifyOnDispatcher(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (ShutdownDispose.IsDispatcherUsable(dispatcher))
            dispatcher!.BeginInvoke(action);
    }
}
