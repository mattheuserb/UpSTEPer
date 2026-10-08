namespace UpSTEPer.Core;

/// <summary>
/// Identifies a face within one loaded <see cref="IStepDocument"/>.
/// </summary>
/// <remarks>
/// Ids are 0-based and dense. They are only valid for the document that produced
/// them and only until its geometry is modified (see docs/adr/0005-topology-identity.md).
/// </remarks>
/// <param name="Value">The 0-based face index.</param>
public readonly record struct FaceId(int Value)
{
    /// <inheritdoc />
    public override string ToString() => $"Face#{Value}";
}
