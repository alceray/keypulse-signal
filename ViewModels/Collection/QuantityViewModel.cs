using System.Globalization;
using KeyPulse.Helpers;
using KeyPulse.Models;

namespace KeyPulse.ViewModels.Collection;

public sealed class QuantityViewModel : ObservableObject
{
    public QuantityViewModel(string kind, int? quantity = null)
    {
        Kind = kind;
        Reset(quantity);
    }

    public string Kind { get; }
    public bool IsSwitch => Kind == PartKinds.Switch;
    public int Maximum => PartKinds.MaxQuantity(Kind);
    public string Unit => IsSwitch ? "switches" : "sets/kits";
    private string _text = "";
    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            Changed();
        }
    }
    public int? Value =>
        int.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 1 && n <= Maximum
            ? n
            : null;
    public string Error => Value.HasValue ? "" : $"Enter a whole number from 1 to {Maximum:N0}.";

    public void Reset(int? quantity = null)
    {
        _text = (quantity ?? (IsSwitch ? 10 : 1)).ToString(CultureInfo.InvariantCulture);
        Changed();
    }

    private void Changed()
    {
        OnPropertyChanged("");
        AsyncRelayCommand.RaiseCanExecuteChanged();
    }
}
