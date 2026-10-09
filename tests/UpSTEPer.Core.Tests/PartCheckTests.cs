namespace UpSTEPer.Core.Tests;

public class PartCheckTests
{
    private static readonly PartCheck _clean = new("Bracket", 1, 12, 0, 0, 4, 0, 0, 0);

    [Fact]
    public void HasProblems_CleanPartWithSplines_IsFalse()
    {
        // Spline faces are no defect in themselves; only canonical ones are worth converting.
        Assert.False(_clean.HasProblems);
    }

    [Fact]
    public void HasProblems_AnyDefect_IsTrue()
    {
        Assert.True((_clean with { FreeEdges = 1 }).HasProblems);
        Assert.True((_clean with { InvalidShapes = 1 }).HasProblems);
        Assert.True((_clean with { CanonicalFaces = 1 }).HasProblems);
        Assert.True((_clean with { MergeableFaces = 1 }).HasProblems);
        Assert.True((_clean with { SmallEdges = 1 }).HasProblems);
        Assert.True((_clean with { FailedChecks = 1 }).HasProblems);
    }

    [Theory]
    [InlineData("Bracket", "Bracket")]
    [InlineData("", "(unnamed part)")]
    [InlineData("  ", "(unnamed part)")]
    public void DisplayName_UsesPlaceholderForUnnamedParts(string name, string expected)
    {
        Assert.Equal(expected, (_clean with { Name = name }).DisplayName);
    }
}
