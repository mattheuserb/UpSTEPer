using System.CommandLine;
using System.Globalization;

namespace UpSTEPer.Cli.Tests;

public class OptionsTests
{
    [Theory]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    public void InvariantDouble_ParsesDotDecimal_RegardlessOfCulture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            var option = Options.InvariantDouble("--value", "test", 1.0);
            var command = new RootCommand { option };

            var result = command.Parse(["--value", "0.05"]);

            Assert.Empty(result.Errors);
            Assert.Equal(0.05, result.GetValue(option));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void InvariantDouble_UsesDefault_WhenOmitted()
    {
        var option = Options.InvariantDouble("--value", "test", 0.2);
        var command = new RootCommand { option };

        Assert.Equal(0.2, command.Parse([]).GetValue(option));
    }

    [Fact]
    public void InvariantDouble_ReportsError_ForNonNumber()
    {
        var option = Options.InvariantDouble("--value", "test", 1.0);
        var command = new RootCommand { option };

        Assert.NotEmpty(command.Parse(["--value", "abc"]).Errors);
    }
}
