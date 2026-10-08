namespace UpSTEPer.Core.Tests;

public class ColorRgbTests
{
    [Theory]
    [InlineData("#C2C9C4", 0xC2, 0xC9, 0xC4)]
    [InlineData("ff0000", 0xFF, 0x00, 0x00)]
    public void FromHex_ParsesBothNotations(string hex, byte r, byte g, byte b)
    {
        Assert.Equal(ColorRgb.FromBytes(r, g, b), ColorRgb.FromHex(hex));
    }

    [Theory]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    public void FromHex_RejectsInvalidInput(string hex)
    {
        Assert.Throws<FormatException>(() => ColorRgb.FromHex(hex));
    }

    [Fact]
    public void ToHex_RoundTrips()
    {
        Assert.Equal("#C2C9C4", ColorRgb.FromHex("#c2c9c4").ToHex());
    }

    [Theory]
    [InlineData(-0.01f)]
    [InlineData(1.01f)]
    [InlineData(float.NaN)]
    public void Constructor_RejectsChannelsOutsideUnitRange(float channel)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColorRgb(channel, 0f, 0f));
    }
}
