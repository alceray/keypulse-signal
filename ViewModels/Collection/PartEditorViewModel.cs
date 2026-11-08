using System.Collections.Immutable;
using System.ComponentModel;
using KeyPulse.Helpers;
using KeyPulse.Models;
using KeyPulse.Services;

namespace KeyPulse.ViewModels.Collection;

public sealed class PartEditorViewModel : ObservableObject, IDataErrorInfo, IDisposable
{
    private readonly CollectionService _collection;
    private CollectionSnapshot? _item;
    private bool _disposed,
        _busy,
        _conflict;
    private string _name = "",
        _manufacturer = "",
        _brand = "",
        _designer = "",
        _variants = "",
        _notes = "";
    private string _type = "Not listed",
        _family = "Not listed",
        _profile = "",
        _material = "",
        _message = "";
    private string _lowProfile = "Not listed";
    private string[] _originalValues = [];
    private string QuantityDraftValue =>
        Quantity.Value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? Quantity.Text;

    private string[] DraftValues() =>
        [
            Name,
            Manufacturer,
            Brand,
            Designer,
            Variants,
            Type,
            Family,
            LowProfile,
            Profile,
            Material,
            QuantityDraftValue,
        ];

    public bool HasChanges =>
        (_item == null && !string.IsNullOrWhiteSpace(Name))
        || HasNotesChanges
        || !DraftValues().SequenceEqual(_originalValues);
    public bool HasNotesChanges => Notes != (_item?.Notes ?? "");
    public bool HasQuantityChanges => QuantityDraftValue != _originalValues[^1];
    public CollectionSnapshot? OriginalItem => _item;
    public CollectionResult? LastResult { get; private set; }

    public void ResetQuantity() => Quantity.Text = _originalValues[^1];

    public void Refresh(CollectionSnapshot item)
    {
        if (!_busy && !HasChanges && !_conflict && _item?.Revision != item.Revision)
            Reset(item);
    }

    public void Reset(CollectionSnapshot item)
    {
        _item = item;
        _conflict = false;
        _message = "";
        Populate(item, "");
    }

    public PartEditorViewModel(
        string kind,
        CollectionService collection,
        CollectionSnapshot? item = null,
        string name = ""
    )
    {
        Kind = kind;
        _collection = collection;
        _item = item;
        Quantity = new(kind, item?.Quantity);
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync(), _ => CanSave);
        ReloadCommand = new AsyncRelayCommand(_ => ReloadAsync(), _ => HasConflict && !_busy);
        Populate(item, name);
        Quantity.PropertyChanged += QuantityChanged;
    }

    public string Kind { get; }
    public bool IsSwitch => Kind == PartKinds.Switch;
    public bool IsKeycap => !IsSwitch;
    public bool CanDescribe => _item?.CatalogEntryId == null;
    public bool IsBusy => _busy;
    public bool IsEditable => !_busy;
    public bool HasConflict => _conflict;
    public string Title => _item == null ? $"Add custom {Kind.ToLowerInvariant()}" : $"Edit {_item.Name}";
    public string DescriptionHint =>
        CanDescribe
            ? "Custom item • stored only in your collection"
            : "Catalog description is read-only. You can change quantity and notes.";
    public QuantityViewModel Quantity { get; }
    public IReadOnlyList<string> Types { get; } =
        ["Not listed", "linear", "tactile", "clicky", "linear/clicky", "latching"];
    public IReadOnlyList<string> Families { get; } =
        ["Not listed", "MX", "Alps", "Choc", "HE", "EC", "Optical", "Legacy", "Other"];
    public IReadOnlyList<string> LowProfileOptions { get; } = ["Not listed", "Yes", "No"];
    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            Changed();
        }
    }
    public string Manufacturer
    {
        get => _manufacturer;
        set
        {
            _manufacturer = value;
            Changed();
        }
    }
    public string Brand
    {
        get => _brand;
        set
        {
            _brand = value;
            Changed();
        }
    }
    public string Designer
    {
        get => _designer;
        set
        {
            _designer = value;
            Changed();
        }
    }
    public string Variants
    {
        get => _variants;
        set
        {
            _variants = value;
            Changed();
        }
    }
    public string Notes
    {
        get => _notes;
        set
        {
            _notes = value;
            Changed();
        }
    }
    public string Type
    {
        get => _type;
        set
        {
            _type = value;
            Changed();
        }
    }
    public string Family
    {
        get => _family;
        set
        {
            _family = value;
            Changed();
        }
    }
    public string Profile
    {
        get => _profile;
        set
        {
            _profile = value;
            Changed();
        }
    }
    public string Material
    {
        get => _material;
        set
        {
            _material = value;
            Changed();
        }
    }
    public string LowProfile
    {
        get => _lowProfile;
        set
        {
            _lowProfile = value;
            Changed();
        }
    }
    public string Message => _message;
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand ReloadCommand { get; }

    private static string? Optional(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private PartDetails Details()
    {
        var variants = Variants
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .ToImmutableArray();
        return IsSwitch
            ? new SwitchPartDetails
            {
                SwitchType = Type == "Not listed" ? null : Optional(Type),
                SwitchFamily = Family == "Not listed" ? null : Optional(Family),
                IsLowProfile = LowProfile switch
                {
                    "Yes" => true,
                    "No" => false,
                    _ => null,
                },
                Variants = variants,
            }
            : new KeycapPartDetails
            {
                Profile = Optional(Profile),
                Material = Optional(Material),
                Variants = variants,
            };
    }

    private PartDescription Description() =>
        new(Name.Trim(), Optional(Manufacturer), Optional(Brand), Optional(Designer), Details());

    public string this[string columnName]
    {
        get
        {
            if (columnName == nameof(Notes))
                return Notes.Length > 2000 ? "Notes can be up to 2,000 characters." : "";
            if (!CanDescribe)
                return "";
            if (columnName == nameof(Name))
                return string.IsNullOrWhiteSpace(Name) ? "Name is required." : "";
            if (columnName is nameof(Variants) or nameof(Type) or nameof(Family))
                try
                {
                    PartDetailsCodec.Validate(Kind, Details(), false);
                }
                catch (ArgumentException ex)
                {
                    return ex.Message;
                }
            return "";
        }
    }
    public string Error =>
        string.Join(
            " ",
            new[] { this[nameof(Name)], this[nameof(Notes)], this[nameof(Variants)], Quantity.Error }.Where(x =>
                x.Length > 0
            )
        );
    public bool CanSave =>
        !_disposed
        && HasChanges
        && !_busy
        && !_conflict
        && _collection.IsLoaded
        && _item?.DetailsError == null
        && Quantity.Value.HasValue
        && Error.Length == 0;

    public async Task<bool> SaveAsync()
    {
        if (!CanSave)
            return false;
        _busy = true;
        _message = "Saving…";
        Changed();
        var result =
            _item == null
                ? await _collection.AddCustomAsync(Kind, Description(), Quantity.Value!.Value, Notes)
                : await _collection.UpdateAsync(
                    _item,
                    Quantity.Value!.Value,
                    Notes,
                    CanDescribe ? Description() : null
                );
        if (_disposed)
            return false;
        _busy = false;
        LastResult = result;
        _conflict = result.Status == CollectionResultStatus.Conflict;
        _message = result.Message ?? "";
        if (result.Succeeded && _item != null)
            Reset(result.Items.Single(x => x.ItemKey == _item.ItemKey));
        Changed();
        return result.Succeeded;
    }

    public async Task<CollectionResult?> SaveNotesAsync()
    {
        if (_disposed || _busy || _conflict || _item == null || !HasNotesChanges || this[nameof(Notes)].Length > 0)
            return null;
        _busy = true;
        Changed();
        // Commit notes only; leave quantity and descriptive drafts untouched.
        var result = await _collection.UpdateAsync(_item, _item.Quantity, Notes);
        if (_disposed)
            return result;
        _busy = false;
        LastResult = result;
        _conflict = result.Status == CollectionResultStatus.Conflict;
        _message = result.Message ?? "";
        if (result.Succeeded)
            _item = result.Items.Single(x => x.ItemKey == _item.ItemKey);
        Changed();
        return result;
    }

    public async Task ReloadAsync()
    {
        if (!_conflict || _busy || _disposed)
            return;
        _busy = true;
        _message = "Reloading current values…";
        Changed();
        var result = await _collection.ReloadAsync();
        if (_disposed)
            return;
        _busy = false;
        if (!result.Succeeded)
            _message = result.Message ?? "Could not reload.";
        else
        {
            var current = result.Items.FirstOrDefault(x => x.ItemKey == _item!.ItemKey);
            if (current == null)
                _message = "This item was removed. Cancel this editor.";
            else if (current.DetailsError != null)
                _message = "Details unavailable. Cancel this editor; the item can still be removed.";
            else
            {
                _item = current;
                Populate(current, "");
                _conflict = false;
                _message = "Reloaded current values. Your previous draft was replaced.";
            }
        }
        Changed();
    }

    private void Populate(CollectionSnapshot? item, string name)
    {
        _name = item?.Name ?? name;
        _manufacturer = item?.Manufacturer ?? "";
        _brand = item?.Brand ?? "";
        _designer = item?.Designer ?? "";
        _notes = item?.Notes ?? "";
        _variants = string.Join(Environment.NewLine, item?.Details?.Variants ?? []);
        _type = (item?.Details as SwitchPartDetails)?.SwitchType ?? "Not listed";
        _family = (item?.Details as SwitchPartDetails)?.SwitchFamily ?? "Not listed";
        _lowProfile = (item?.Details as SwitchPartDetails)?.IsLowProfile switch
        {
            true => "Yes",
            false => "No",
            _ => "Not listed",
        };
        _profile = (item?.Details as KeycapPartDetails)?.Profile ?? "";
        _material = (item?.Details as KeycapPartDetails)?.Material ?? "";
        Quantity.Reset(item?.Quantity);
        _originalValues = DraftValues();
        Changed();
    }

    private void Changed()
    {
        OnPropertyChanged("");
        AsyncRelayCommand.RaiseCanExecuteChanged();
    }

    private void QuantityChanged(object? sender, PropertyChangedEventArgs args) => Changed();

    public void Dispose()
    {
        _disposed = true;
        Quantity.PropertyChanged -= QuantityChanged;
    }
}
