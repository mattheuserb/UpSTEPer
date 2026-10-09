using System.Globalization;
using UpSTEPer.Core;

namespace UpSTEPer.Cli;

/// <summary>
/// Shows progress as one self-overwriting line ("Transferring  45 %"), cleared when an operation
/// completes. Use only for an interactive terminal: redirected output would collect every update.
/// </summary>
/// <remarks>Reports may arrive from native worker threads; writes are serialised.</remarks>
internal sealed class ConsoleProgress(TextWriter writer) : IProgress<OperationProgress>
{
    private const int LineWidth = 40;

    private readonly Lock _lock = new();

    /// <inheritdoc />
    public void Report(OperationProgress value)
    {
        var text = value.Fraction >= 1.0
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $"{value.Phase} {value.Fraction * 100,3:0} %");

        lock (_lock)
        {
            writer.Write('\r' + text.PadRight(LineWidth) + (text.Length == 0 ? "\r" : string.Empty));
            writer.Flush();
        }
    }
}
