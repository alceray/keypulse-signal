using System.Windows;
using System.Windows.Controls;
using KeyPulse.Models;
using KeyPulse.Services;
using KeyPulse.ViewModels.Collection;
using Microsoft.Extensions.DependencyInjection;

namespace KeyPulse.Views.Collection;

public partial class CollectionView : UserControl, IDisposable
{
    private bool _initialized,
        _disposed;
    public PartListViewModel Switches { get; }
    public PartListViewModel Keycaps { get; }

    public CollectionView()
        : this(
            App.ServiceProvider.GetRequiredService<CollectionService>(),
            App.ServiceProvider.GetRequiredService<CatalogService>()
        ) { }

    public CollectionView(CollectionService collection, CatalogService catalogs)
    {
        Switches = new(PartKinds.Switch, collection, catalogs);
        Keycaps = new(PartKinds.Keycap, collection, catalogs);
        InitializeComponent();
        DataContext = this;
        Loaded += Initialize;
    }

    private async void Initialize(object sender, RoutedEventArgs e)
    {
        if (_initialized || _disposed)
            return;
        _initialized = true;
        await Task.WhenAll(Switches.InitializeAsync(), Keycaps.InitializeAsync());
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Loaded -= Initialize;
        Switches.Dispose();
        Keycaps.Dispose();
    }
}
