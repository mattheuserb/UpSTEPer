using System.CommandLine;
using UpSTEPer.Core;

namespace UpSTEPer.Cli.Tests;

public sealed class PaintCommandTests : IDisposable
{
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private FakeStepDocument? _document;

    [Fact]
    public void Paint_AppliesBodyThenFaceColoursAndSaves()
    {
        var exitCode = Invoke("paint", "in.stp", "out.stp", "--face", "1=#FF0000", "--body", "0=#3366CC", "--face", "2=none");

        Assert.Equal(0, exitCode);
        Assert.Equal(ColorRgb.FromHex("#3366CC"), _document!.AssignedColors[new ColorTarget(ColorTargetKind.Body, 0)]);
        Assert.Equal(ColorRgb.FromHex("#FF0000"), _document.AssignedColors[new ColorTarget(ColorTargetKind.Face, 1)]);
        Assert.Null(_document.AssignedColors[new ColorTarget(ColorTargetKind.Face, 2)]);
        Assert.Equal(Path.GetFullPath("out.stp"), _document.SavedPath);
        Assert.True(_document.IsDisposed);

        var lines = _output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["Set Body 0 = #3366CC", "Set Face 1 = #FF0000", "Set Face 2 = none", $"Saved {Path.GetFullPath("out.stp")} (Ap214)"], lines);
        Assert.Equal(StepSchema.Ap214, _document.SavedSchema);
    }

    [Theory]
    [InlineData("ap242", StepSchema.Ap242)]
    [InlineData("Ap214", StepSchema.Ap214)]
    public void Paint_SchemaOption_SelectsProtocol(string value, StepSchema expected)
    {
        var exitCode = Invoke("paint", "in.stp", "out.stp", "--face", "1=#FF0000", "--schema", value);

        Assert.Equal(0, exitCode);
        Assert.Equal(expected, _document!.SavedSchema);
    }

    [Fact]
    public void Paint_WithoutAssignments_FailsWithInputError()
    {
        var exitCode = Invoke("paint", "in.stp", "out.stp");

        Assert.Equal(CommandRunner.InputError, exitCode);
        Assert.Contains("Nothing to do", _error.ToString(), StringComparison.Ordinal);
        Assert.Null(_document);
    }

    [Fact]
    public void Paint_MalformedAssignment_FailsWithInputError()
    {
        var exitCode = Invoke("paint", "in.stp", "out.stp", "--face", "80=red");

        Assert.Equal(CommandRunner.InputError, exitCode);
        Assert.Contains("#RRGGBB", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Paint_UnknownFace_FailsWithInputErrorAndDoesNotSave()
    {
        var exitCode = Invoke("paint", "in.stp", "out.stp", "--face", "99=#FF0000");

        Assert.Equal(CommandRunner.InputError, exitCode);
        Assert.Null(_document!.SavedPath);
    }

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
    }

    private int Invoke(params string[] args)
    {
        var reader = new FakeStepReader(path => _document = new FakeStepDocument(path));
        var root = new RootCommand { PaintCommand.Create(reader, _output, _error) };
        return root.Parse(args).Invoke();
    }
}
