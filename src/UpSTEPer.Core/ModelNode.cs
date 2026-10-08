namespace UpSTEPer.Core;

/// <summary>Kind of a node in the product structure.</summary>
public enum ModelNodeKind
{
    /// <summary>An assembly (top level or an instance inside another assembly).</summary>
    Assembly = 0,

    /// <summary>A part (simple shape), possibly an instance inside an assembly.</summary>
    Part = 1,

    /// <summary>A body (solid) of its parent part.</summary>
    Body = 2,
}

/// <summary>A node of the product structure (assemblies → parts → bodies).</summary>
/// <param name="Name">Display name from the STEP file; may be empty.</param>
/// <param name="Kind">Assembly, part or body.</param>
/// <param name="Body">The body for <see cref="ModelNodeKind.Body"/> nodes, else <see langword="null"/>.</param>
/// <param name="Children">Child nodes.</param>
public sealed record ModelNode(string Name, ModelNodeKind Kind, BodyId? Body, IReadOnlyList<ModelNode> Children)
{
    /// <summary>Gets all bodies in this subtree (the node itself and its descendants).</summary>
    /// <returns>The bodies in depth-first order.</returns>
    public IEnumerable<BodyId> GetBodies()
    {
        if (Body is { } body)
        {
            yield return body;
        }

        foreach (var body2 in Children.SelectMany(c => c.GetBodies()))
        {
            yield return body2;
        }
    }

    /// <summary>Gets the name, or a placeholder for unnamed nodes.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"({Kind.ToString().ToUpperInvariant()})" : Name;
}
