using UpSTEPer.Core;

namespace UpSTEPer.Cli.Tests;

public class ConsoleProgressTests
{
    [Fact]
    public void Report_OverwritesOneLineAndClearsItWhenDone()
    {
        using var writer = new StringWriter();
        var progress = new ConsoleProgress(writer);

        progress.Report(new OperationProgress(0.456, "Transferring"));
        progress.Report(new OperationProgress(1.0, "Transferring"));

        var text = writer.ToString();
        Assert.StartsWith("\rTransferring  46 %", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', text);
        Assert.EndsWith("\r" + new string(' ', 40) + "\r", text, StringComparison.Ordinal);
    }
}
