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

    [Theory]
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
