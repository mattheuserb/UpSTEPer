namespace UpSTEPer.Core;

/// <summary>Topology counts of a loaded model (unique entities, shared ones counted once).</summary>
/// <param name="SolidCount">Number of solids.</param>
/// <param name="ShellCount">Number of shells.</param>
/// <param name="FaceCount">Number of faces.</param>
/// <param name="EdgeCount">Number of edges.</param>
/// <param name="VertexCount">Number of vertices.</param>
public sealed record ModelStatistics(int SolidCount, int ShellCount, int FaceCount, int EdgeCount, int VertexCount);
