namespace UpSTEPer.Core;

/// <summary>Loads STEP files (AP203, AP214, AP242).</summary>
public interface IStepReader
{
    /// <summary>Loads a STEP file including colours, names and assembly structure.</summary>
    /// <param name="path">Path to a <c>.stp</c>/<c>.step</c> file.</param>
    /// <param name="progress">
    /// Receives progress reports, possibly from another thread; may be <see langword="null"/>.
    /// Parsing the file reports no intermediate progress and cannot be interrupted.
    /// </param>
    /// <param name="cancellationToken">Cancels loading; no document is returned then.</param>
    /// <returns>The loaded document; the caller owns and disposes it.</returns>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="StepImportException">The file is not valid STEP or contains no geometry.</exception>
    /// <exception cref="OperationCanceledException">Loading was cancelled.</exception>
    IStepDocument Load(string path, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default);
}
