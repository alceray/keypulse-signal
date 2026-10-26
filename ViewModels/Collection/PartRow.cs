using System.Collections.ObjectModel;
using System.Globalization;
using KeyPulse.Helpers;
using KeyPulse.Models;

namespace KeyPulse.ViewModels.Collection;

public sealed class PartRow(string kind, string identity) : ObservableObject
{
    public CatalogEntry? Entry { get; private set; }
    public CollectionSnapshot? Item { get; private set; }
    public string Identity { get; } = identity;
    public bool IsNew => Item == null && Entry == null;
    public string Section => Item == null && !IsNew ? "Catalog" : "Your collection";
    public string Name => Item?.Name ?? Entry?.Description.Name ?? "New custom item";
    public PartEditorViewModel? Editor { get; private set; }
    public bool IsEditing => Editor != null;
    public bool IsDescribing => Editor?.CanDescribe == true;
    public bool CanAdd => !IsNew && Item == null && CanEditQuantity && !IsEditing && !HasQuantityDraft;
    public bool CanAdjust => Item is { DetailsError: null } && !IsEditing && !HasQuantityDraft;
    public bool CanRemove => Item != null && !IsEditing;
    public bool CanEdit => Item is { DetailsError: null } && !IsEditing && !HasQuantityDraft;

    public void SetEditor(PartEditorViewModel? editor)
    {
        Editor?.Dispose();
        Editor = editor;
        OnPropertyChanged("");
    }

    public string? Manufacturer => Item != null ? Item.Manufacturer : Entry?.Description.Manufacturer;
    public string? Brand => Item != null ? Item.Brand : Entry?.Description.Brand;
    public string? Designer => Item != null ? Item.Designer : Entry?.Description.Designer;
    public PartDetails? Details => Item != null ? Item.Details : Entry?.Description.Details;
    public string Variants => string.Join(", ", Details?.Variants ?? []);
    public string? Type => (Details as SwitchPartDetails)?.SwitchType;
    public string? Family => (Details as SwitchPartDetails)?.SwitchFamily;
    public string LowProfile =>
        (Details as SwitchPartDetails)?.IsLowProfile switch
        {
            true => "Yes",
            false => "No",
            _ => "Not listed",
        };
    public string? Profile => (Details as KeycapPartDetails)?.Profile;
    public string? Material => (Details as KeycapPartDetails)?.Material;
    public int Quantity => Item?.Quantity ?? 0;
    public string? Notes => Item?.Notes;
    public DateTime? AddedAt => Item?.AddedAt;
    public string Status { get; private set; } = "";
    public bool CanEditQuantity { get; private set; }
    public string SearchText { get; private set; } = "";
    private string? _searchFields;
    private string _quantityText = "0";
    public bool HasQuantityDraft { get; private set; }
    public CollectionSnapshot? QuantityOriginal { get; private set; }
    public string QuantityText
    {
        get => _quantityText;
        set
        {
            if (value == _quantityText)
                return;
            if (!HasQuantityDraft)
                QuantityOriginal = Item;
            HasQuantityDraft = true;
            _quantityText = value;
            OnPropertyChanged("");
        }
    }
    public int? QuantityValue =>
        int.TryParse(QuantityText, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
        && value >= 0
        && value <= PartKinds.MaxQuantity(kind)
            ? value
            : null;
    public string QuantityError =>
        QuantityValue.HasValue ? "" : $"Use a whole number from 0 to {PartKinds.MaxQuantity(kind):N0}.";
    public bool CanSaveQuantity => HasQuantityDraft && QuantityValue.HasValue;

    public void ResetQuantity()
    {
        HasQuantityDraft = false;
        QuantityOriginal = null;
        _quantityText = Quantity.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged("");
    }

    public bool Update(
        CollectionSnapshot? item,
        CatalogEntry? entry,
        string status,
        CatalogSearch search,
        bool catalogReady,
        string? catalogSearchText = null
    )
    {
        var canEdit = item?.DetailsError == null && (item != null || catalogReady);
        if (
            Item?.ItemKey == item?.ItemKey
            && Item?.Revision == item?.Revision
            && ReferenceEquals(Entry, entry)
            && Status == status
            && CanEditQuantity == canEdit
        )
            return false;
        Item = item;
        Entry = entry;
        Status = status;
        CanEditQuantity = canEdit;
        if (!HasQuantityDraft)
            _quantityText = Quantity.ToString(CultureInfo.InvariantCulture);
        if (item == null)
        {
            SearchText = catalogSearchText!;
            _searchFields = null;
        }
        else
        {
            var fields = string.Join(
                "\0",
                Name,
                Manufacturer,
                Brand,
                Designer,
                Variants,
                Type,
                Family,
                Profile,
                Material,
                Notes
            );
            if (fields != _searchFields)
            {
                SearchText = search.Build(item);
                _searchFields = fields;
            }
        }
        OnPropertyChanged("");
        return true;
    }
}

public sealed class PartFilter(string label, Func<PartRow, string?> read) : ObservableObject
{
    public const string Any = "Any",
        NotListed = "Not listed";
    public string Label { get; } = label;
    public ObservableCollection<string> Options { get; } = [Any];
    private string _selected = Any;
    public string Selected
    {
        get => _selected;
        set
        {
            if (value == null || _selected == value)
                return;
            _selected = value;
            OnPropertyChanged();
        }
    }

    public bool Matches(PartRow row) =>
        Selected == Any || string.Equals(Selected, read(row) ?? NotListed, StringComparison.OrdinalIgnoreCase);

    public void UpdateOptions(IEnumerable<PartRow> rows)
    {
        var options = rows.Select(x => read(x) ?? NotListed)
            .Append(Selected)
            .Where(x => x != Any)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Prepend(Any)
            .ToArray();
        if (Options.SequenceEqual(options))
            return;
        // Never remove the selected option: ComboBox would clear its selection during a refresh.
        for (var i = Options.Count - 1; i >= 0; i--)
            if (!options.Contains(Options[i]))
                Options.RemoveAt(i);
        for (var i = 0; i < options.Length; i++)
            if (!Options.Contains(options[i]))
                Options.Insert(i, options[i]);
    }
}
