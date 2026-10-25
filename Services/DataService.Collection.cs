using System.Collections.Immutable;
using KeyPulse.Data;
using KeyPulse.Helpers;
using KeyPulse.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace KeyPulse.Services;

public partial class DataService
{
    private readonly CatalogService _catalogs;
    internal object CollectionGate { get; } = new();

    // Cache reads must never wait for database work; writers acquire CollectionGate first.
    private readonly object _collectionCacheLock = new();
    private ImmutableArray<CollectionSnapshot> _collectionItems = [];
    private bool _collectionLoaded;
    public ImmutableArray<CollectionSnapshot> CollectionItems
    {
        get
        {
            lock (_collectionCacheLock)
                return _collectionItems;
        }
    }
    public bool CollectionLoaded
    {
        get
        {
            lock (_collectionCacheLock)
                return _collectionLoaded;
        }
    }

    public CollectionResult ReadCollectionItems()
    {
        lock (CollectionGate)
        {
            try
            {
                using var context = _factory.CreateDbContext();
                var items = ReadSnapshots(context);
                lock (_collectionCacheLock)
                {
                    _collectionItems = items;
                    _collectionLoaded = true;
                }
                return new(CollectionResultStatus.Success, items);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not read the parts collection");
                return CollectionResult.Failure(
                    CollectionResultStatus.StorageFailure,
                    "Could not read your collection. Retry or check logs for details."
                );
            }
        }
    }

    public CollectionResult AddCatalogCollectionItem(string kind, string id, int quantity) =>
        WriteCollection(context =>
        {
            ValidateQuantity(kind, quantity);
            var catalog = _catalogs.GetState(kind);
            if (catalog.Status != CatalogLoadStatus.Loaded || !catalog.ById.TryGetValue(id, out var entry))
                throw new ArgumentException("Choose an entry from a loaded catalog.");
            var row = context.CollectionItems.SingleOrDefault(x => x.PartKind == kind && x.CatalogEntryId == id);
            if (row != null)
            {
                RequireReadable(row);
                ValidateQuantity(kind, (long)row.Quantity + quantity);
                row.Quantity += quantity;
                row.Revision = checked(row.Revision + 1);
            }
            else
            {
                row = new CollectionItem
                {
                    PartKind = kind,
                    CatalogEntryId = id,
                    QuantityUnit = PartKinds.Unit(kind),
                    Quantity = quantity,
                };
                SetDescription(row, entry.Description);
                context.CollectionItems.Add(row);
            }
        });

    public CollectionResult AddCustomCollectionItem(
        string kind,
        PartDescription description,
        int quantity,
        string? notes
    ) =>
        WriteCollection(context =>
        {
            ValidateQuantity(kind, quantity);
            ValidateNotes(notes);
            var row = new CollectionItem
            {
                PartKind = kind,
                QuantityUnit = PartKinds.Unit(kind),
                Quantity = quantity,
                Notes = notes,
            };
            SetDescription(row, description);
            CheckCustomName(context, row);
            context.CollectionItems.Add(row);
        });

    public CollectionResult UpdateCollectionItem(
        Guid key,
        long revision,
        int quantity,
        string? notes,
        PartDescription? customDescription = null
    ) =>
        WriteCollection(context =>
        {
            var row = context.CollectionItems.SingleOrDefault(x => x.ItemKey == key);
            CheckRevision(row, revision);
            RequireReadable(row!);
            ValidateQuantity(row!.PartKind, quantity);
            ValidateNotes(notes);
            if (customDescription != null)
            {
                if (row!.CatalogEntryId != null)
                    throw new ArgumentException("Catalog descriptions are read-only.");
                SetDescription(row!, customDescription);
                CheckCustomName(context, row!);
            }
            row!.Quantity = quantity;
            row.Notes = notes;
            context.ChangeTracker.DetectChanges();
            if (context.Entry(row).State == EntityState.Modified)
                row.Revision = checked(row.Revision + 1);
        });

    public CollectionResult RemoveCollectionItem(Guid key, long revision) =>
        WriteCollection(context =>
        {
            var row = context.CollectionItems.SingleOrDefault(x => x.ItemKey == key);
            if (row == null)
                return;
            CheckRevision(row, revision);
            context.CollectionItems.Remove(row);
        });

    public CollectionResult RefreshCollectionSnapshots(string kind) =>
        WriteCollection(context =>
        {
            var catalog = _catalogs.GetState(kind);
            if (catalog.Status != CatalogLoadStatus.Loaded)
                throw new ArgumentException("The catalog is unavailable.");
            foreach (
                var row in context.CollectionItems.Where(x => x.PartKind == kind && x.CatalogEntryId != null).ToList()
            )
            {
                var snapshot = PartDetailsCodec.Snapshot(row);
                if (snapshot.Details == null || !catalog.ById.TryGetValue(row.CatalogEntryId!, out var entry))
                    continue;
                var normalized = PartDetailsCodec.Serialize(kind, snapshot.Details, true);
                var incoming = PartDetailsCodec.Serialize(kind, entry.Description.Details, true);
                var description = entry.Description;
                if (
                    row.Name == description.Name
                    && row.Manufacturer == description.Manufacturer
                    && row.Brand == description.Brand
                    && row.Designer == description.Designer
                    && normalized == incoming
                )
                    continue;
                SetDescription(row, description);
                row.Revision = checked(row.Revision + 1);
            }
        });

    private CollectionResult WriteCollection(Action<ApplicationDbContext> write)
    {
        lock (CollectionGate)
        {
            if (!CollectionLoaded)
                return CollectionResult.Failure(
                    CollectionResultStatus.ValidationError,
                    "Load your collection before making changes."
                );
            try
            {
                using var context = _factory.CreateDbContext();
                using var transaction = context.Database.BeginTransaction();
                write(context);
                context.SaveChanges();
                var committed = ReadSnapshots(context);
                transaction.Commit();
                lock (_collectionCacheLock)
                    _collectionItems = committed;
                return new(CollectionResultStatus.Success, committed);
            }
            catch (DbUpdateConcurrencyException)
            {
                return CollectionResult.Failure(
                    CollectionResultStatus.Conflict,
                    "This item changed. Reload before saving."
                );
            }
            catch (ArgumentException ex)
            {
                return CollectionResult.Failure(CollectionResultStatus.ValidationError, ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not save the parts collection");
                return CollectionResult.Failure(
                    CollectionResultStatus.StorageFailure,
                    "Could not update your collection. Check logs for details."
                );
            }
        }
    }

    private static ImmutableArray<CollectionSnapshot> ReadSnapshots(ApplicationDbContext context) =>
        context
            .CollectionItems.AsNoTracking()
            .ToList()
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ItemKey)
            .Select(PartDetailsCodec.Snapshot)
            .ToImmutableArray();

    private static void SetDescription(CollectionItem row, PartDescription description)
    {
        PartDetailsCodec.ValidateText(description.Name, true);
        PartDetailsCodec.ValidateText(description.Manufacturer);
        PartDetailsCodec.ValidateText(description.Brand);
        PartDetailsCodec.ValidateText(description.Designer);
        var json = PartDetailsCodec.Serialize(row.PartKind, description.Details, row.CatalogEntryId != null);
        var existing = row.CollectionItemId == 0 ? null : PartDetailsCodec.Snapshot(row).Details;
        if (existing == null || PartDetailsCodec.Serialize(row.PartKind, existing, row.CatalogEntryId != null) != json)
            row.DetailsJson = json;
        row.Name = description.Name;
        row.Manufacturer = description.Manufacturer;
        row.Brand = description.Brand;
        row.Designer = description.Designer;
    }

    private static void ValidateQuantity(string kind, long value)
    {
        var maximum = PartKinds.MaxQuantity(kind);
        if (value < 1 || value > maximum)
            throw new ArgumentException($"Enter a whole number from 1 to {maximum:N0}.");
    }

    private static void ValidateNotes(string? value)
    {
        if (value?.Length > 2000)
            throw new ArgumentException("Notes can be up to 2,000 characters.");
    }

    private static void CheckRevision(CollectionItem? row, long revision)
    {
        if (row == null || row.Revision != revision)
            throw new DbUpdateConcurrencyException();
    }

    private static void RequireReadable(CollectionItem row)
    {
        if (PartDetailsCodec.Snapshot(row).Details == null)
            throw new ArgumentException("This item's details are unavailable.");
    }

    private static void CheckCustomName(ApplicationDbContext context, CollectionItem row)
    {
        var name = CatalogSearch.Normalize(row.Name);
        if (
            context
                .CollectionItems.Where(x =>
                    x.PartKind == row.PartKind && x.CatalogEntryId == null && x.ItemKey != row.ItemKey
                )
                .Select(x => x.Name)
                .AsEnumerable()
                .Any(x => CatalogSearch.Normalize(x) == name)
        )
            throw new ArgumentException($"A custom {row.PartKind.ToLowerInvariant()} named {row.Name} already exists.");
    }
}
