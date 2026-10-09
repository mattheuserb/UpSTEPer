namespace UpSTEPer.Core;

/// <summary>Progress of a long operation such as loading or meshing a model.</summary>
/// <param name="Fraction">Overall progress from 0 to 1; never decreases within one operation.</param>
/// <param name="Phase">Short English label of the current step, e.g. "Reading", "Transferring", "Meshing".</param>
public readonly record struct OperationProgress(double Fraction, string Phase);
