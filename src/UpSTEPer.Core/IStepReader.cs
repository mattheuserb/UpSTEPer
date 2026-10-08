namespace UpSTEPer.Core;

/// <summary>Loads STEP files (AP203, AP214, AP242).</summary>
public interface IStepReader
{
    /// <summary>Loads a STEP file including colours, names and assembly structure.</summary>
    /// <param name="path">Path to a <c>.stp</c>/<c>.step</c> file.</param>
    /// <returns>The loaded document; the caller owns and disposes it.</returns>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="StepImportException">The file is not valid STEP or contains no geometry.</exception>
    IStepDocument Load(string path);
}
