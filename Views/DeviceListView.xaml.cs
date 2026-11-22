using System.Windows.Controls;
using System.Windows.Input;
using KeyPulse.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace KeyPulse.Views;

public partial class DeviceListView
{
    public DeviceListView()
        : this(App.ServiceProvider.GetRequiredService<DeviceListViewModel>()) { }

    internal DeviceListView(object model)
    {
        InitializeComponent();
        DataContext = model;
    }

    private void DataGridRow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow row)
            return;

        row.IsSelected = true;
        row.Focus();
    }
}
