using System.CommandLine;
using System.Globalization;
using UpSTEPer.Core;

namespace UpSTEPer.Cli;

/// <summary><c>upsteper info &lt;file&gt;</c>: prints topology, colours and tessellation statistics.</summary>
internal static class InfoCommand
{
    public static Command Create(IStepReader reader, TextWriter output, TextWriter error, IProgress<OperationProgress>? progress = null)
    {
        var fileArgument = new Argument<FileInfo>("file") { Description = "STEP file (.stp/.step) to inspect." };
        var linearOption = Options.InvariantDouble(
            "--linear-deflection", "Tessellation chord tolerance in mm.", TessellationOptions.Default.LinearDeflection);
        var angularOption = Options.InvariantDouble(
            "--angular-deflection", "Tessellation angular tolerance in radians.", TessellationOptions.Default.AngularDeflectionRadians);

        var command = new Command("info", "Print topology, colour and mesh statistics of a STEP file.")
        {
            fileArgument,
            linearOption,
            angularOption,
        };

        // The cancellation token is signalled by Ctrl+C; the native kernel polls it.
        command.SetAction((parseResult, cancellationToken) => Task.FromResult(CommandRunner.Run(error, () =>
        {
            var file = parseResult.GetValue(fileArgument)!;
            var options = new TessellationOptions(parseResult.GetValue(linearOption), parseResult.GetValue(angularOption));
            return Run(reader, output, file.FullName, options, progress, cancellationToken);
        })));

        return command;
    }

    private static int Run(
        IStepReader reader,
        TextWriter output,
        string path,
        TessellationOptions options,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var document = reader.Load(path, progress, cancellationToken);
        var stats = document.Statistics;

        output.WriteLine($"File:      {document.SourcePath}");
        output.WriteLine($"Solids:    {stats.SolidCount}");
        output.WriteLine($"Shells:    {stats.ShellCount}");
        output.WriteLine($"Faces:     {stats.FaceCount}");
        output.WriteLine($"Edges:     {stats.EdgeCount}");
        output.WriteLine($"Vertices:  {stats.VertexCount}");

        var colours = Enumerable.Range(0, stats.FaceCount)
            .Select(i => document.GetFaceColor(new FaceId(i)))
            .GroupBy(c => c?.ToHex() ?? "(none)")
            .OrderByDescending(g => g.Count());
        output.WriteLine("Colours:");
        foreach (var group in colours)
        {
            output.WriteLine($"  {group.Key,-8} {group.Count()} faces");
        }

        var mesh = document.Tessellate(options, progress, cancellationToken);
        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Mesh:      {mesh.TriangleCount} triangles, {mesh.VertexCount} vertices (linear {options.LinearDeflection} mm, angular {options.AngularDeflectionRadians} rad)"));
        return 0;
    }
}
