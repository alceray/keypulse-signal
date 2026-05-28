using KeyPulse.Helpers;

namespace KeyPulse.Tests.Helpers;

/// <summary>
/// In a headless test process Application.Current is null, so OnPropertyChanged invokes
/// synchronously — letting us cover the notification contract without a WPF dispatcher.
/// </summary>
public class ObservableObjectTests
{
    private sealed class Probe : ObservableObject
    {
        private int _value;

        public int Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged();
            }
        }

        public void Raise(string? name) => OnPropertyChanged(name);
    }

    [Fact]
    public void OnPropertyChanged_UsesCallerMemberName()
    {
        var probe = new Probe();
        var names = new List<string?>();
        probe.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        probe.Value = 42;

        names.ShouldHaveSingleItem().ShouldBe(nameof(Probe.Value));
    }

    [Fact]
    public void OnPropertyChanged_NoSubscribers_DoesNotThrow() => Should.NotThrow(() => new Probe().Value = 1);

    [Fact]
    public void OnPropertyChanged_PassesNameVerbatim()
    {
        var probe = new Probe();
        string? captured = "unset";
        probe.PropertyChanged += (_, e) => captured = e.PropertyName;

        probe.Raise(null);

        captured.ShouldBeNull();
    }
}
