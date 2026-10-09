using System.CommandLine;
using UpSTEPer.Core;

namespace UpSTEPer.Cli.Tests;

public sealed class RepairCommandTests : IDisposable
{
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private FakeStepDocument? _document;

    [Fact]
    public void Repair_PrintsReportAndSaves()
    {
        var exitCode = Invoke(
            [
                new PartRepair("Unsewn box", true, false, 6, 6, 0, 1, 24, 0, 0, 0, 0),
                new PartRepair("Healthy box", false, false, 6, 6, 1, 1, 0, 0, 0, 0, 0),
                new PartRepair("Broken", false, true, 9, 9, 1, 1, 0, 0, 2, 2, 0),
                new PartRepair("Fragile", false, false, 9, 9, 1, 1, 0, 0, 1, 1, 0, Reverted: true),
            ],
            "repair", "in.stp", "out.stp", "--schema", "ap242");

        Assert.Equal(0, exitCode);
        Assert.Equal(RepairOptions.Default, _document!.RepairedWith);
        Assert.Equal(Path.GetFullPath("out.stp"), _document.SavedPath);
        Assert.Equal(StepSchema.Ap242, _document.SavedSchema);
        var lines = _output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Matches(@"^Unsewn box\s+repaired\s+6\s+0->1\s+24->0\s+0\s+0$", lines[1]);
        Assert.Matches(@"^Healthy box\s+unchanged\s+6\s+1\s+0\s+0\s+0$", lines[2]);
        Assert.Matches(@"^Broken\s+unchanged\s+.*\s+a step failed$", lines[3]);
        Assert.Matches(@"^Fragile\s+unchanged\s+.*\s+a step made it worse, discarded$", lines[4]);
        Assert.Equal("1 of 4 part(s) repaired; steps discarded as worse in 1, failed in 1.", lines[5]);
    }

    [Fact]
    public void Repair_StepsAndTolerance_ArePassedOn()
    {
        Invoke([], "repair", "in.stp", "out.stp", "--steps", "sew, MERGE", "--tolerance", "0.2");

        Assert.Equal(new RepairOptions(RepairSteps.Sew | RepairSteps.MergeFaces, 0.2), _document!.RepairedWith);
    }

    [Theory]
    [InlineData("fix", RepairSteps.Fix)]
    [InlineData("fix,sew,merge", RepairSteps.All)]
    [InlineData(" Sew ", RepairSteps.Sew)]
    public void ParseSteps_AcceptsNamesCaseInsensitive(string text, RepairSteps expected)
    {
        Assert.Equal(expected, RepairCommand.ParseSteps(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("heal")]
    [InlineData(",")]
    public void ParseSteps_InvalidText_Throws(string text)
    {
        Assert.Throws<FormatException>(() => RepairCommand.ParseSteps(text));
    }

    [Fact]
    public void Repair_UnknownStep_FailsWithInputErrorAndDoesNotSave()
    {
        var exitCode = Invoke([], "repair", "in.stp", "out.stp", "--steps", "heal");

        Assert.Equal(CommandRunner.InputError, exitCode);
        Assert.Contains("Unknown repair step 'heal'", _error.ToString(), StringComparison.Ordinal);
        Assert.Null(_document?.SavedPath);
    }

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
    }

    private int Invoke(IReadOnlyList<PartRepair> result, params string[] args)
    {
        var reader = new FakeStepReader(path => _document = new FakeStepDocument(path) { RepairResult = result });
        var root = new RootCommand { RepairCommand.Create(reader, _output, _error) };
        return root.Parse(args).Invoke();
    }
}
