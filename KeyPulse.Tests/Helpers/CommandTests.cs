using KeyPulse.Helpers;

namespace KeyPulse.Tests.Helpers;

public class RelayCommandTests
{
    [Fact]
    public void CanExecute_NullPredicate_ReturnsTrue() => new RelayCommand(_ => { }).CanExecute(null).ShouldBeTrue();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CanExecute_UsesPredicate(bool allowed) =>
        new RelayCommand(_ => { }, _ => allowed).CanExecute(null).ShouldBe(allowed);

    [Fact]
    public void Execute_InvokesWithParameter()
    {
        object? received = null;
        new RelayCommand(p => received = p).Execute("hello");
        received.ShouldBe("hello");
    }

    [Fact]
    public void Execute_NullDelegate_Throws() =>
        Should.Throw<ArgumentNullException>(() => new RelayCommand(null!).Execute(null));
}

public class AsyncRelayCommandTests
{
    [Fact]
    public async Task CanExecute_FalseWhileExecuting_ResetAfter()
    {
        var gate = new TaskCompletionSource();
        var cmd = new AsyncRelayCommand(_ => gate.Task);

        var running = cmd.ExecuteAsync(null);
        cmd.CanExecute(null).ShouldBeFalse(); // gated mid-flight

        gate.SetResult();
        await running;
        cmd.CanExecute(null).ShouldBeTrue(); // reset after completion
    }

    [Fact]
    public async Task ExecuteAsync_WhenCannotExecute_DoesNotInvoke()
    {
        var invoked = false;
        var cmd = new AsyncRelayCommand(
            _ =>
            {
                invoked = true;
                return Task.CompletedTask;
            },
            _ => false
        );
        await cmd.ExecuteAsync(null);
        invoked.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_FaultingDelegate_ResetsStateAndRethrows()
    {
        var cmd = new AsyncRelayCommand(_ => throw new InvalidOperationException("boom"));
        await Should.ThrowAsync<InvalidOperationException>(() => cmd.ExecuteAsync(null));
        cmd.CanExecute(null).ShouldBeTrue(); // _isExecuting reset in finally
    }

    [Fact]
    public void Execute_FaultingDelegate_DoesNotThrowSynchronously() =>
        Should.NotThrow(() => new AsyncRelayCommand(_ => throw new InvalidOperationException()).Execute(null));
}
