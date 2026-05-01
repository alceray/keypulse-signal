using System.Windows;
using KeyPulse.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace KeyPulse.Views;

public partial class DashboardView
{
    public DashboardView()
    {
        InitializeComponent();
        DataContext = App.ServiceProvider.GetRequiredService<DashboardViewModel>();
    }
}
