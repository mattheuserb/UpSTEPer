using System.CommandLine;
using UpSTEPer.Core.Drawing;

namespace UpSTEPer.Cli.Tests;

public sealed class ExportDxfCommandTests : IDisposable
{
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"upsteper-{Guid.NewGuid():N}.dxf");

    [Theory]
    [InlineData("front,top,left,iso", new[] { ViewDirection.Front, ViewDirection.Top, ViewDirection.Left, ViewDirection.Iso })]
    [InlineData(" Iso , BACK ", new[] { ViewDirection.Iso, ViewDirection.Back })]
    public void ParseViews_AcceptsNamesInAnyCase(string text, ViewDirection[] expected)
    {
        Assert.Equal(expected, ExportDxfCommand.ParseViews(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("front,side")]
    [InlineData("front,front")]
    [InlineData("1")]
    public void ParseViews_Invalid_ThrowsFormatException(string text)
    {
        Assert.Throws<FormatException>(() => ExportDxfCommand.ParseViews(text));
    }

    [Fact]
    public void ExportDxf_WithoutViews_ExportsFrontTopLeftButNoIso()
    {
        var exitCode = Invoke("export-dxf", "in.stp", _path);

        Assert.Equal(0, exitCode);
        var views = _output.ToString().Split(Environment.NewLine).Where(l => l.EndsWith(" curves", StringComparison.Ordinal)).Select(l => l.Split(' ')[0]);
        Assert.Equal(["Front", "Top", "Left"], views);
    }

    [Fact]
    public void ExportDxf_WritesFileAndReportsViews()
    {
        var exitCode = Invoke("export-dxf", "in.stp", _path, "--views", "front,iso", "--gap", "12.5");

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(_path));
        var text = _output.ToString();
        Assert.Contains("Front", text, StringComparison.Ordinal);
        Assert.Contains("Iso", text, StringComparison.Ordinal);
        Assert.Contains($"Saved {_path}", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportDxf_UnknownView_FailsWithInputError()
    {
        var exitCode = Invoke("export-dxf", "in.stp", _path, "--views", "sideways");

        Assert.Equal(CommandRunner.InputError, exitCode);
        Assert.Contains("Unknown view 'sideways'", _error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(_path));
    }

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
        File.Delete(_path);
    }

    private int Invoke(params string[] args)
    {
        var reader = new FakeStepReader(path => new FakeStepDocument(path));
        var root = new RootCommand { ExportDxfCommand.Create(reader, _output, _error) };
        return root.Parse(args).Invoke();
    }
}
