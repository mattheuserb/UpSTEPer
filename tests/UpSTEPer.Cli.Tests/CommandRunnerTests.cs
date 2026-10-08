using UpSTEPer.Core;

namespace UpSTEPer.Cli.Tests;

public class CommandRunnerTests
{
    [Fact]
    public void Run_ExpectedFailure_PrintsMessageAndReturnsInputError()
    {
        using var error = new StringWriter();

        var exitCode = CommandRunner.Run(error, () => throw new StepImportException("not a STEP file"));

        Assert.Equal(CommandRunner.InputError, exitCode);
        Assert.Equal("error: not a STEP file", error.ToString().Trim());
    }

    [Fact]
    public void Run_Success_ReturnsBodyExitCode()
    {
        using var error = new StringWriter();

        Assert.Equal(0, CommandRunner.Run(error, () => 0));
        Assert.Empty(error.ToString());
    }

    [Fact]
    public void Run_ArgumentOutOfRange_ShowsOnlyTheUserMessage()
    {
        using var error = new StringWriter();

        CommandRunner.Run(error, () => throw new ArgumentOutOfRangeException("target", 500, "Face 500 does not exist."));

        Assert.Equal("error: Face 500 does not exist.", error.ToString().Trim());
    }

    [Fact]
    public void UserMessage_KeepsMessagesWithoutParameterSuffix()
    {
        Assert.Equal("plain", CommandRunner.UserMessage(new IOException("plain")));
        Assert.Equal("no param", CommandRunner.UserMessage(new ArgumentException("no param")));
    }

    [Fact]
    public void Run_UnexpectedException_Propagates()
    {
        using var error = new StringWriter();

        Assert.Throws<InvalidOperationException>(() => CommandRunner.Run(error, () => throw new InvalidOperationException()));
    }
}
