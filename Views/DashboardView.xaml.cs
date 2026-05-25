using System.Windows.Input;
using KeyPulse.ViewModels;
using KeyPulse.ViewModels.Dashboard;
using Microsoft.Extensions.DependencyInjection;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Wpf;

namespace KeyPulse.Views;

public partial class DashboardView
{
    public DashboardView()
    {
        InitializeComponent();
        DataContext = App.ServiceProvider.GetRequiredService<DashboardViewModel>();
    }

    // Hand cursor over clickable parts (slices/lines). Tunneling Preview event because OxyPlot
    // consumes the bubbling MouseMove.
    private void PlotView_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not PlotView plotView || plotView.ActualModel is not { } model)
            return;

        var position = e.GetPosition(plotView);
        var point = new ScreenPoint(position.X, position.Y);

        var overClickable = model.Series.OfType<PieSeries>().Any()
            ? DashboardPieChartBuilder.GetSliceAt(model, point) != null
            : model.GetSeriesFromPoint(point, DashboardActivityChartBuilder.LineHitTolerance) is LineSeries;

        plotView.Cursor = overClickable ? Cursors.Hand : Cursors.Arrow;
    }
}
