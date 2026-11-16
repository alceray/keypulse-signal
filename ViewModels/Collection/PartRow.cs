using System.Collections.ObjectModel;
using System.ComponentModel;
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
    public string Section => Item == null && !IsNew && !IsAddingVariants ? "Catalog" : "My collection";
    public string Name => Item?.Name ?? Entry?.Description.Name ?? "New custom item";
    public string Variant => Item?.Variant ?? "";
    public string Title => Item == null ? Name : TitleOf(Item);
    public IReadOnlyList<VariantOption> VariantOptions { get; private set; } = [];
    public bool IsAddingVariants => Item == null && VariantOptions.Count > 0 && HasQuantityDraft;
    public bool HasVariantChoices => Entry != null && VariantOptions.Count > 0 && (Item != null || IsAddingVariants);
    public IReadOnlyList<string> SelectedVariants =>
        VariantOptions.Where(x => x.IsSelected).Select(x => x.Label).ToArray();
    public string VariantSummary => Item != null ? Variant : string.Join(", ", SelectedVariants);

    internal static string TitleOf(CollectionSnapshot item) =>
        item.Variant.Length > 0 ? $"{item.Name} {item.Variant}" : item.Name;

    internal bool SetVariantOptions(IReadOnlyList<string> labels, IReadOnlySet<string> owned)
    {
        foreach (var option in VariantOptions)
            option.IsSelected = owned.Contains(option.Label);
        if (VariantOptions.Select(x => x.Label).SequenceEqual(labels))
            return false;
        VariantOptions = labels.Select(x => new VariantOption(x, owned.Contains(x), VariantsChanged)).ToArray();
        OnPropertyChanged("");
        return true;
    }

    public void ClearVariantSelection()
    {
        foreach (var option in VariantOptions)
            option.IsSelected = false;
    }

    private void VariantsChanged() => OnPropertyChanged(nameof(VariantSummary));

    public PartEditorViewModel? Editor { get; private set; }
    public bool IsEditing => IsNew || HasQuantityDraft || Editor is { HasChanges: true } or { HasConflict: true };
    public bool HasEditor => Editor != null;
    public bool ShowQuantityActions => !IsNew && HasQuantityDraft;
    public bool ShowCustomActions =>
        IsNew || IsDescribing && Editor is { HasDescriptionChanges: true } or { HasConflict: true };
    public bool IsDescribing => Editor?.CanDescribe == true;
    public bool CanAdd => !IsNew && Item == null && CanEditQuantity && !IsEditing && !HasQuantityDraft;
    public bool CanAdjust =>
        (IsNew || CanEditQuantity && (Item != null || HasQuantityDraft))
        && Editor?.HasConflict != true
        && Editor?.IsBusy != true;
    public bool CanDecrease => CanAdjust && QuantityValue > 0;
    public bool IsPendingRemoval => Item != null && HasQuantityDraft && QuantityValue == 0;
    public bool CanRemove => Item != null && !HasQuantityDraft;

    public void SetEditor(PartEditorViewModel? editor)
    {
        if (Editor != null)
            Editor.PropertyChanged -= EditorChanged;
        Editor?.Dispose();
        Editor = editor;
        if (Editor != null)
            Editor.PropertyChanged += EditorChanged;
        OnPropertyChanged("");
    }

    private void EditorChanged(object? sender, PropertyChangedEventArgs args) => OnPropertyChanged("");

    public string? Manufacturer => Item != null ? Item.Manufacturer : Entry?.Description.Manufacturer;
    public string? Brand => Item != null ? Item.Brand : Entry?.Description.Brand;
    public string? Designer => Item != null ? Item.Designer : Entry?.Description.Designer;
    public PartDetails? Details => Item != null ? Item.Details : Entry?.Description.Details;
    public string Variants => Item != null && Variant.Length > 0 ? Variant : string.Join(", ", Details?.Variants ?? []);
    public string? Type => FormatSwitchType((Details as SwitchPartDetails)?.SwitchType);

    internal static string? FormatSwitchType(string? type) =>
        type == null ? null : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(type.ToLowerInvariant());

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
    public bool CanEnterQuantity => IsNew || CanEditQuantity;
    public string SearchText { get; private set; } = "";
    private string? _searchFields;
    private string _quantityText = "0";
    private bool _hasQuantityDraft;
    private CollectionSnapshot? _quantityOriginal;
    public bool HasQuantityDraft => Editor?.HasQuantityChanges ?? _hasQuantityDraft;
    public CollectionSnapshot? QuantityOriginal => Editor?.OriginalItem ?? _quantityOriginal;
    public string QuantityText
    {
        get => Editor?.Quantity.Text ?? _quantityText;
        set
        {
            if (Editor != null)
            {
                if (Editor.Quantity.Text != value)
                    Editor.Quantity.Text = value;
                return;
            }
            if (value == _quantityText)
                return;
            if (!HasQuantityDraft)
                _quantityOriginal = Item;
            _hasQuantityDraft = true;
            _quantityText = value;
            if (QuantityValue == (QuantityOriginal?.Quantity ?? 0))
            {
                ResetQuantity();
                return;
            }
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
    public bool CanSaveQuantity =>
        !IsNew && HasQuantityDraft && QuantityValue.HasValue && Editor?.HasConflict != true && Editor?.IsBusy != true;

    public void ResetQuantity()
    {
        _hasQuantityDraft = false;
        _quantityOriginal = null;
        _quantityText = Quantity.ToString(CultureInfo.InvariantCulture);
        Editor?.ResetQuantity();
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

public sealed class VariantOption(string label, bool selected, Action changed) : ObservableObject
{
    private bool _isSelected = selected;
    public string Label { get; } = label;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            OnPropertyChanged();
            changed();
        }
    }
}

public sealed class PartFilter(string label, Func<PartRow, string?> read) : ObservableObject
{
    public const string Any = "",
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
