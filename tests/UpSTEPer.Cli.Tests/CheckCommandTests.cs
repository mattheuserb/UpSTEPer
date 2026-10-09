using System.CommandLine;
using UpSTEPer.Core;

namespace UpSTEPer.Cli.Tests;

public sealed class CheckCommandTests : IDisposable
{
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    [Fact]
    public void Check_WithProblems_PrintsTableAndReturnsProblemsFound()
    {
        var exitCode = Invoke(
            [
                new PartCheck("Healthy box", 1, 6, 0, 0, 0, 0, 0, 0),
                new PartCheck("Unsewn box", 0, 6, 24, 0, 0, 0, 0, 0),
                new PartCheck(string.Empty, 1, 3, 0, 0, 3, 3, 0, 0),
            ],
            "check", "model.stp");

        Assert.Equal(CheckCommand.ProblemsFound, exitCode);
        var lines = _output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("Part ", lines[0], StringComparison.Ordinal);
        Assert.Matches(@"^Unsewn box\s+0\s+6\s+24\s+0\s+0 \(0\)\s+0\s+0\s+0$", lines[2]);
        Assert.Matches(@"^\(unnamed part\)\s+1\s+3\s+0\s+0\s+3 \(3\)\s+0\s+0\s+0$", lines[3]);
        Assert.Equal("2 of 3 part(s) have problems.", lines[^1]);
        Assert.Empty(_error.ToString());
    }

    [Fact]
    public void Check_NoProblems_ReturnsZero()
    {
        var exitCode = Invoke([new PartCheck("Healthy box", 1, 6, 0, 0, 0, 0, 0, 0)], "check", "model.stp");

        Assert.Equal(0, exitCode);
        Assert.EndsWith($"No problems found in 1 part(s).{Environment.NewLine}", _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Check_InvalidTolerance_FailsWithUsageErrorDistinctFromProblems()
    {
        // System.CommandLine reports invalid arguments with 1; "problems found" must differ.
        var exitCode = Invoke([], "check", "model.stp", "--tolerance", "abc");

        Assert.Equal(1, exitCode);
        Assert.NotEqual(CheckCommand.ProblemsFound, exitCode);
    }

    [Fact]
    public void Check_FailedChecks_CountAsProblems()
    {
        var exitCode = Invoke([new PartCheck("Broken", 1, 9, 0, 0, 0, 0, 0, 0, FailedChecks: 2)], "check", "model.stp");

        Assert.Equal(CheckCommand.ProblemsFound, exitCode);
        Assert.Matches(@"^Broken\s+1\s+9\s+0\s+0\s+0 \(0\)\s+0\s+0\s+2$", _output.ToString().Split(Environment.NewLine)[1]);
    }

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
    }

    private int Invoke(IReadOnlyList<PartCheck> result, params string[] args)
    {
        var reader = new FakeStepReader(path => new FakeStepDocument(path) { CheckResult = result });
        var root = new RootCommand { CheckCommand.Create(reader, _output, _error) };
        return root.Parse(args).Invoke();
    }
}
