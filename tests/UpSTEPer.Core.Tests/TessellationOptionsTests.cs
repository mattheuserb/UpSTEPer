namespace UpSTEPer.Core.Tests;

public class TessellationOptionsTests
{
    [Theory]
    [InlineData(0, 0.1)]
    [InlineData(-1, 0.1)]
    [InlineData(0.1, 0)]
    [InlineData(double.NaN, 0.1)]
    [InlineData(0.1, double.PositiveInfinity)]
    public void Constructor_RejectsNonPositiveOrNonFiniteValues(double linear, double angular)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TessellationOptions(linear, angular));
    }

    [Fact]
    public void Presets_AreOrderedFromCoarseToFine()
    {
        Assert.True(TessellationOptions.Coarse.AngularDeflectionRadians > TessellationOptions.Default.AngularDeflectionRadians);
        Assert.True(TessellationOptions.Default.AngularDeflectionRadians > TessellationOptions.Fine.AngularDeflectionRadians);
    }
}
