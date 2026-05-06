using KeyPulse.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace KeyPulse.Views;

public partial class DeviceListView
{
    public DeviceListView()
    {
        InitializeComponent();
        DataContext = App.ServiceProvider.GetRequiredService<DeviceListViewModel>();
    }
}
