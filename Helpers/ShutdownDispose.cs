using Serilog;

namespace KeyPulse.Helpers;

public static class ShutdownDispose
{
    public static void TryStep(Action? action, string stepName)
    {
        if (action == null)
            return;

        try
        {
            action();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to {StepName}", stepName);
        }
    }

    public static bool IsProcessTearingDown(System.Windows.Threading.Dispatcher? dispatcher = null)
    {
        return Environment.HasShutdownStarted
            || AppDomain.CurrentDomain.IsFinalizingForUnload()
            || dispatcher == null
            || dispatcher.HasShutdownStarted
            || dispatcher.HasShutdownFinished;
    }

    public static bool IsDispatcherUsable(System.Windows.Threading.Dispatcher? dispatcher)
    {
        return dispatcher != null && !dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished;
    }
}
