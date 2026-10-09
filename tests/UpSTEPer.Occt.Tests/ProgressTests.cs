using UpSTEPer.Core;

namespace UpSTEPer.Occt.Tests;

public class ProgressTests
{
    private readonly OcctStepReader _reader = new();

    [Fact]
    public void Load_ReportsPhasesAndMonotonicProgressEndingAtOne()
    {
        TestFiles.RequireNativeLibrary();
        var progress = new RecordingProgress();

        using var document = _reader.Load(TestFiles.CableGland, progress, TestContext.Current.CancellationToken);

        var reports = progress.Reports;
        Assert.NotEmpty(reports);
        Assert.All(reports.Zip(reports.Skip(1)), pair => Assert.True(pair.Second.Fraction >= pair.First.Fraction, $"{pair.First} -> {pair.Second}"));
        Assert.InRange(reports[0].Fraction, 0, 0.01);
        Assert.Equal(1.0, reports[^1].Fraction);
        Assert.Equal(["Reading", "Transferring"], reports.Select(r => r.Phase).Distinct());
    }

    [Fact]
    public void Load_CancelledFromProgressHandler_ThrowsAndReturnsNoDocument()
    {
        TestFiles.RequireNativeLibrary();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var progress = new RecordingProgress(_ => cancellation.Cancel());

        var exception = Assert.ThrowsAny<OperationCanceledException>(
            () => _reader.Load(TestFiles.CableGland, progress, cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.DoesNotContain(progress.Reports, r => r.Fraction >= 1.0);
    }

    [Fact]
    public void Load_AlreadyCancelled_ThrowsBeforeReading()
    {
        TestFiles.RequireNativeLibrary();
        var progress = new RecordingProgress();

        Assert.ThrowsAny<OperationCanceledException>(
            () => _reader.Load(TestFiles.CableGland, progress, new CancellationToken(canceled: true)));

        Assert.Empty(progress.Reports);
    }

    [Fact]
    public void Load_ProgressHandlerThrows_RethrowsItAfterNativeCall()
    {
        TestFiles.RequireNativeLibrary();
        var progress = new RecordingProgress(_ => throw new InvalidOperationException("handler failed"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => _reader.Load(TestFiles.CableGland, progress, TestContext.Current.CancellationToken));

        Assert.Equal("handler failed", exception.Message);
    }

    [Fact]
    public void Tessellate_ReportsMeshingProgressEndingAtOne()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);
        var progress = new RecordingProgress();

        document.Tessellate(TessellationOptions.Fine, progress, TestContext.Current.CancellationToken);

        Assert.All(progress.Reports, r => Assert.Equal("Meshing", r.Phase));
        Assert.Equal(1.0, progress.Reports[^1].Fraction);
    }

    [Fact]
    public void Tessellate_Cancelled_ThrowsAndDocumentCanBeMeshedAgain()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        Assert.ThrowsAny<OperationCanceledException>(
            () => document.Tessellate(TessellationOptions.Fine, new RecordingProgress(_ => cancellation.Cancel()), cancellation.Token));

        var mesh = document.Tessellate(TessellationOptions.Default, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(document.Statistics.FaceCount, mesh.TriangleFaceIds.ToArray().Distinct().Count());
    }

    // Records synchronously (Progress<T> would post to a thread-pool context and arrive late).
    private sealed class RecordingProgress(Action<OperationProgress>? onReport = null) : IProgress<OperationProgress>
    {
        private readonly List<OperationProgress> _reports = [];

        public IReadOnlyList<OperationProgress> Reports
        {
            get
            {
                lock (_reports)
                {
                    return [.. _reports];
                }
            }
        }

        public void Report(OperationProgress value)
        {
            lock (_reports)
            {
                _reports.Add(value);
            }

            onReport?.Invoke(value);
        }
    }
}
