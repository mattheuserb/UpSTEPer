using System.CommandLine;
using UpSTEPer.Core;

namespace UpSTEPer.Cli.Tests;

public sealed class InfoCommandTests : IDisposable
{
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    [Fact]
    public void Info_PrintsStatisticsColoursAndMesh()
    {
        FakeStepDocument? document = null;
        var reader = new FakeStepReader(path => document = new FakeStepDocument(path));

        var exitCode = Invoke(reader, "info", "model.stp");

        Assert.Equal(0, exitCode);
        var text = _output.ToString();
        Assert.Contains("Faces:     3", text, StringComparison.Ordinal);
        Assert.Contains("#FF0000  2 faces", text, StringComparison.Ordinal);
        Assert.Contains("(none)   1 faces", text, StringComparison.Ordinal);
        Assert.Contains("Mesh:      3 triangles, 9 vertices", text, StringComparison.Ordinal);
        Assert.True(document!.IsDisposed);
        Assert.Empty(_error.ToString());
    }

    [Fact]
    public void Info_PassesFullPathAndTessellationOptions()
    {
        FakeStepDocument? document = null;
        var reader = new FakeStepReader(path => document = new FakeStepDocument(path));

        Invoke(reader, "info", "model.stp", "--linear-deflection", "0.01", "--angular-deflection", "0.05");

        Assert.Equal(Path.GetFullPath("model.stp"), Assert.Single(reader.LoadedPaths));
        Assert.Equal(0.01, document!.LastOptions!.LinearDeflection);
        Assert.Equal(0.05, document.LastOptions.AngularDeflectionRadians);
    }

    [Fact]
    public void Info_UsesDefaultTessellationOptions()
    {
        FakeStepDocument? document = null;
        var reader = new FakeStepReader(path => document = new FakeStepDocument(path));

        Invoke(reader, "info", "model.stp");

        Assert.Equal(TessellationOptions.Default.LinearDeflection, document!.LastOptions!.LinearDeflection);
        Assert.Equal(TessellationOptions.Default.AngularDeflectionRadians, document.LastOptions.AngularDeflectionRadians);
    }

    [Fact]
    public void Info_ImportFailure_ReportsErrorWithInputErrorExitCode()
    {
        var reader = new FakeStepReader(_ => throw new StepImportException("Cannot read STEP file"));

        var exitCode = Invoke(reader, "info", "broken.stp");

        Assert.Equal(CommandRunner.InputError, exitCode);
        Assert.Equal("error: Cannot read STEP file", _error.ToString().Trim());
        Assert.Empty(_output.ToString());
    }

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
    }

    [Fact]
    public async Task Info_PassesProgressAndCancellationToReader()
    {
        var reader = new FakeStepReader(path => new FakeStepDocument(path));
        var progress = new Progress<OperationProgress>();
        using var cancellation = new CancellationTokenSource();

        var root = new RootCommand { InfoCommand.Create(reader, _output, _error, progress) };
        await root.Parse(["info", "model.stp"]).InvokeAsync(cancellationToken: cancellation.Token);

        Assert.Same(progress, reader.LastProgress);
        Assert.True(reader.LastCancellationToken.CanBeCanceled);
    }

    [Fact]
    public async Task Info_CancelledBeforeLoading_ReturnsCancelledExitCode()
    {
        var reader = new FakeStepReader(path => new FakeStepDocument(path));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var root = new RootCommand { InfoCommand.Create(reader, _output, _error) };
        var exitCode = await root.Parse(["info", "model.stp"]).InvokeAsync(cancellationToken: cancellation.Token);

        Assert.Equal(CommandRunner.Cancelled, exitCode);
    }

    private int Invoke(IStepReader reader, params string[] args)
    {
        var root = new RootCommand { InfoCommand.Create(reader, _output, _error) };
        return root.Parse(args).Invoke();
    }
}
