using System.Windows;
using KeyPulse.ViewModels;

namespace KeyPulse.Views;

public partial class DevicePartsEditorWindow : Window
{
    public DevicePartsEditorWindow(DevicePartsEditorViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Loaded += async (_, _) => await model.ReloadAsync();
        Closed += (_, _) => model.Dispose();
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
