using UpSTEPer.Core;

namespace UpSTEPer.Cli.Tests;

public class ColorAssignmentTests
{
    [Fact]
    public void Parse_IdAndHexColour()
    {
        var assignment = ColorAssignment.Parse("80=#FF0000", ColorTargetKind.Face);

        Assert.Equal(new ColorTarget(ColorTargetKind.Face, 80), assignment.Target);
        Assert.Equal(ColorRgb.FromHex("#FF0000"), assignment.Color);
    }

    [Theory]
    [InlineData("3=none")]
    [InlineData(" 3 = NONE ")]
    public void Parse_None_RemovesColour(string text)
    {
        var assignment = ColorAssignment.Parse(text, ColorTargetKind.Body);

        Assert.Equal(new ColorTarget(ColorTargetKind.Body, 3), assignment.Target);
        Assert.Null(assignment.Color);
    }

    [Theory]
    [InlineData("80")]
    [InlineData("=#FF0000")]
    [InlineData("-1=#FF0000")]
    [InlineData("x=#FF0000")]
    [InlineData("80=red")]
    public void Parse_Malformed_ThrowsFormatException(string text)
    {
        Assert.Throws<FormatException>(() => ColorAssignment.Parse(text, ColorTargetKind.Face));
    }

    [Fact]
    public void ToString_IsReadable()
    {
        Assert.Equal("Face 80 = #FF0000", ColorAssignment.Parse("80=#ff0000", ColorTargetKind.Face).ToString());
        Assert.Equal("Body 0 = none", ColorAssignment.Parse("0=none", ColorTargetKind.Body).ToString());
    }
}
