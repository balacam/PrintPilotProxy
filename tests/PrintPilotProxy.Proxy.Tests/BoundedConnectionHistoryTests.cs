using PrintPilotProxy.Proxy;
using Xunit;

namespace PrintPilotProxy.Proxy.Tests;

public sealed class BoundedConnectionHistoryTests
{
    [Fact]
    public void HighConnectionChurnKeepsOnlyTheConfiguredHistory()
    {
        var history = new BoundedConnectionHistory(128);
        var oldest = Guid.NewGuid();
        Assert.True(history.TryAdd(oldest));
        Assert.False(history.TryAdd(oldest));
        for (var i = 0; i < 100_000; i++) history.TryAdd(Guid.NewGuid());
        Assert.Equal(128, history.Count);
        Assert.True(history.TryAdd(oldest));
        Assert.Equal(128, history.Count);
    }

    [Fact]
    public void ConcurrentDuplicateConnectionsAreLoggedOnlyOnce()
    {
        var history = new BoundedConnectionHistory(16);
        var id = Guid.NewGuid();
        var added = 0;
        Parallel.For(0, 1000, _ => { if (history.TryAdd(id)) Interlocked.Increment(ref added); });
        Assert.Equal(1, added);
        Assert.Equal(1, history.Count);
        history.Clear();
        Assert.Equal(0, history.Count);
        Assert.True(history.TryAdd(id));
    }

    [Fact]
    public void ConcurrentChurnCannotExceedTheCapacity()
    {
        var history = new BoundedConnectionHistory(16);
        Parallel.For(0, 10_000, _ => history.TryAdd(Guid.NewGuid()));
        Assert.Equal(16, history.Count);
    }
}
