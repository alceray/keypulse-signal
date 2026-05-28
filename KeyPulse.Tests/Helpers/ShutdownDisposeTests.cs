using KeyPulse.Helpers;

namespace KeyPulse.Tests.Helpers;

/// <summary>
/// Covers the dispatcher-independent paths of ShutdownDispose. Branches requiring a real or
/// shutting-down WPF Dispatcher are not deterministically unit-testable.
/// </summary>
public class ShutdownDisposeTests
{
    [Fact]
    public void TryStep_NullAction_DoesNothing() => Should.NotThrow(() => ShutdownDispose.TryStep(null, "noop"));

    [Fact]
    public void TryStep_RunsAction()
    {
        var ran = false;
        ShutdownDispose.TryStep(() => ran = true, "step");
        ran.ShouldBeTrue();
    }

    [Fact]
    public void TryStep_SwallowsExceptions() =>
        Should.NotThrow(() => ShutdownDispose.TryStep(() => throw new InvalidOperationException("boom"), "throwing"));

    [Fact]
    public void TryStep_NullStepName_DoesNotThrow() => Should.NotThrow(() => ShutdownDispose.TryStep(() => { }, null!));

    [Fact]
    public void IsDispatcherUsable_NullDispatcher_ReturnsFalse() =>
        ShutdownDispose.IsDispatcherUsable(null).ShouldBeFalse();

    [Fact]
    public void IsProcessTearingDown_NullDispatcher_ReturnsTrue() =>
        ShutdownDispose.IsProcessTearingDown(null).ShouldBeTrue();
}
