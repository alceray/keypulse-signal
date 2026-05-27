using System.Windows.Controls;
using System.Windows.Input;
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

    private void DataGridRow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow row)
            return;

        row.IsSelected = true;
        row.Focus();
    }
}
