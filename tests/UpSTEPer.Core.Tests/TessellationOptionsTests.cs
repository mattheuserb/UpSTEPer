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

    [Theory]
    [InlineData(DisplayQuality.Coarse, 0.2, 0.5, 0.2)]
    [InlineData(DisplayQuality.Normal, 0.05, 0.2, 0.1)]
    [InlineData(DisplayQuality.Fine, 0.01, 0.1, 0.05)]
    public void ForModel_ScalesToleranceAndMinSizeWithModelSize(DisplayQuality quality, double linearAt100, double angular, double minSizeAt100)
    {
        // Diagonal of a 60 x 80 x 0 box: 100 mm.
        var options = TessellationOptions.ForModel(quality, new ModelBounds(0, 0, 0, 60, 80, 0));
        var large = TessellationOptions.ForModel(quality, new ModelBounds(0, 0, 0, 6000, 8000, 0));

        Assert.Equal(linearAt100, options.LinearDeflection, 9);
        Assert.Equal(linearAt100 * 100, large.LinearDeflection, 6);
        Assert.Equal(angular, options.AngularDeflectionRadians);
        Assert.Equal(minSizeAt100, options.MinSize!.Value, 9);
        Assert.Equal(minSizeAt100 * 100, large.MinSize!.Value, 6);
        Assert.False(options.IsRelative);
    }

    [Fact]
    public void MinSize_DefaultsToKernelChoice_AndRejectsInvalidValues()
    {
        Assert.Null(TessellationOptions.Default.MinSize);
        Assert.Equal(0.5, (TessellationOptions.Default with { MinSize = 0.5 }).MinSize);
        Assert.Throws<ArgumentOutOfRangeException>(() => TessellationOptions.Default with { MinSize = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => TessellationOptions.Default with { MinSize = double.NaN });
    }

    [Fact]
    public void ForModel_TinyModel_KeepsMinimumChordTolerance()
    {
        var options = TessellationOptions.ForModel(DisplayQuality.Fine, new ModelBounds(0, 0, 0, 0, 0, 0));

        Assert.Equal(1e-3, options.LinearDeflection);
    }

    [Fact]
    public void ForModel_UnknownBounds_UsesFixedPresets()
    {
        Assert.Same(TessellationOptions.Coarse, TessellationOptions.ForModel(DisplayQuality.Coarse, null));
        Assert.Same(TessellationOptions.Default, TessellationOptions.ForModel(DisplayQuality.Normal, null));
        Assert.Same(TessellationOptions.Fine, TessellationOptions.ForModel(DisplayQuality.Fine, null));
    }

    [Fact]
    public void ForModel_UndefinedQuality_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TessellationOptions.ForModel((DisplayQuality)7, null));
    }

    [Fact]
    public void ModelBounds_Diagonal_IsLengthOfBoxDiagonal()
    {
        Assert.Equal(13.0, new ModelBounds(-1, -2, -3, 2, 2, 9).Diagonal, 12);
    }
}
