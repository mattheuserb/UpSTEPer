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

    [Fact]
    public void Parse_HoverFace()
    {
        Assert.Equal(7, AppOptions.Parse(["model.stp", "--hover-face", "7"]).HoverFace);
    }

    [Fact]
    public void Parse_WindowScreenshotAndLanguage()
    {
        var options = AppOptions.Parse(["model.stp", "--screenshot", "out.png", "--window", "--language", "EN"]);

        Assert.True(options.WindowScreenshot);
        Assert.Equal("en", options.Language);
        Assert.False(AppOptions.Parse(["model.stp"]).WindowScreenshot);
        Assert.Null(AppOptions.Parse(["model.stp"]).Language);
    }

    [Theory]
    [InlineData("model.stp", "--hover-face", "x")]
    [InlineData("model.stp", "--window")]
    [InlineData("model.stp", "--language")]
    [InlineData("model.stp", "--language", "fr")]
    [InlineData("--hover-face", "3")]
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
