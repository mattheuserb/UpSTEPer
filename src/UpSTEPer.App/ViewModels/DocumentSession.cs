using UpSTEPer.Core;

namespace UpSTEPer.App.ViewModels;

/// <summary>
/// Owns an open <see cref="IStepDocument"/> and runs every operation on it one at a time on the
/// thread pool, so the UI never blocks and the (not thread-safe) document is never used concurrently.
/// </summary>
internal sealed class DocumentSession : IDisposable
{
    private readonly IStepDocument _document;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="DocumentSession"/> class.</summary>
    /// <param name="document">The document; the session takes ownership.</param>
    public DocumentSession(IStepDocument document) => _document = document ?? throw new ArgumentNullException(nameof(document));

    /// <summary>Runs <paramref name="operation"/> on the document after all previously queued operations.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The operation.</param>
    /// <returns>The result.</returns>
    public async Task<T> RunAsync<T>(Func<IStepDocument, T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(() => operation(_document)).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Disposes the document once queued operations have finished.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gate.Wait();
        try
        {
            _document.Dispose();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}
