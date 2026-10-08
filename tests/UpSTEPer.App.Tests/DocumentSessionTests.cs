using UpSTEPer.App.ViewModels;

namespace UpSTEPer.App.Tests;

public class DocumentSessionTests
{
    [Fact]
    public async Task RunAsync_ExecutesOperationsOneAtATime()
    {
        using var document = new FakeDocument();
        using var session = new DocumentSession(document);
        var running = 0;
        var maxConcurrent = 0;

        var tasks = Enumerable.Range(0, 20).Select(_ => session.RunAsync(_ =>
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref maxConcurrent, now);
            Thread.Sleep(2);
            Interlocked.Decrement(ref running);
            return now;
        }));
        await Task.WhenAll(tasks);

        Assert.Equal(1, maxConcurrent);
    }

    [Fact]
    public async Task RunAsync_PropagatesExceptions()
    {
        using var document = new FakeDocument();
        using var session = new DocumentSession(document);

        await Assert.ThrowsAsync<IOException>(() => session.RunAsync<int>(_ => throw new IOException("boom")));

        Assert.Equal(2, await session.RunAsync(open => open.Statistics.FaceCount));
    }

    [Fact]
    public async Task Dispose_DisposesDocumentAndRejectsFurtherWork()
    {
        using var document = new FakeDocument();
        using var session = new DocumentSession(document);

        session.Dispose();
        session.Dispose();

        Assert.True(document.IsDisposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.RunAsync(_ => 0));
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }
}
