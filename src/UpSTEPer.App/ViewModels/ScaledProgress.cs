using UpSTEPer.Core;

namespace UpSTEPer.App.ViewModels;

/// <summary>
/// Maps the 0..1 progress of one step onto a section of an overall progress, e.g. loading to
/// 0..0.8 and meshing to 0.8..1 of "open a model".
/// </summary>
/// <param name="target">Receives the mapped reports.</param>
/// <param name="from">Overall fraction at the start of the step.</param>
/// <param name="to">Overall fraction at the end of the step.</param>
internal sealed class ScaledProgress(IProgress<OperationProgress> target, double from, double to) : IProgress<OperationProgress>
{
    /// <inheritdoc />
    public void Report(OperationProgress value) =>
        target.Report(value with { Fraction = from + (Math.Clamp(value.Fraction, 0, 1) * (to - from)) });
}
