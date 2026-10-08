namespace UpSTEPer.Core;

/// <summary>
/// Identifies a body (solid) within one loaded <see cref="IStepDocument"/>.
/// </summary>
/// <remarks>Same lifetime rules as <see cref="FaceId"/> (docs/adr/0005-topology-identity.md).</remarks>
/// <param name="Value">The 0-based body index.</param>
public readonly record struct BodyId(int Value)
{
    /// <inheritdoc />
    public override string ToString() => $"Body#{Value}";
}
