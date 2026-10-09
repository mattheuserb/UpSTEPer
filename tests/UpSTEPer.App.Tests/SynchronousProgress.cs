using UpSTEPer.Core;

namespace UpSTEPer.App.Tests;

/// <summary>Reports synchronously on the calling thread (unlike <see cref="Progress{T}"/>).</summary>
internal sealed class SynchronousProgress(Action<OperationProgress> report) : IProgress<OperationProgress>
{
    public void Report(OperationProgress value) => report(value);
}
