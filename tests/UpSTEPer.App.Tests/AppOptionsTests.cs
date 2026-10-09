using UpSTEPer.Core;
using UpSTEPer.Core.Drawing;

namespace UpSTEPer.App.Tests;

public class AppOptionsTests
{
    [Fact]
    public void Parse_NoArguments_ReturnsDefault()
    {
        Assert.Equal(AppOptions.Default, AppOptions.Parse([]));
    }

    [Fact]
    public void Parse_FileScreenshotAndSelection()
    {
        var options = AppOptions.Parse(["model.stp", "--screenshot", "out.png", "--select-face", "12"]);

        Assert.Equal(new AppOptions("model.stp", "out.png", 12), options);
    }

    [Fact]
    public void Parse_ViewAndOrtho()
    {
        var options = AppOptions.Parse(["model.stp", "--view", "Top", "--ortho"]);

        Assert.Equal(new AppOptions("model.stp", null, null, ViewDirection.Top, Orthographic: true), options);
    }

    [Fact]
    public void Parse_Quality()
    {
        var options = AppOptions.Parse(["model.stp", "--quality", "fine"]);

        Assert.Equal(DisplayQuality.Fine, options.Quality);
        Assert.Equal(DisplayQuality.Normal, AppOptions.Parse(["model.stp"]).Quality);
    }

    [Theory]
    [InlineData("model.stp", "--quality")]
    [InlineData("model.stp", "--quality", "ultra")]
    [InlineData("model.stp", "--quality", "2")]
    [InlineData("model.stp", "--view")]
    [InlineData("model.stp", "--view", "diagonal")]
    [InlineData("model.stp", "--view", "1")]
    [InlineData("--screenshot")]
    [InlineData("model.stp", "--screenshot")]
    [InlineData("model.stp", "--select-face", "-1")]
    [InlineData("model.stp", "--select-face", "abc")]
    [InlineData("--screenshot", "out.png")]
    [InlineData("--select-face", "3")]
    public void Parse_InvalidArguments_Throw(params string[] args)
    {
        Assert.Throws<ArgumentException>(() => AppOptions.Parse(args));
    }
}
