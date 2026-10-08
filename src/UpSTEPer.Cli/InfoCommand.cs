using System.CommandLine;
using System.Globalization;
using UpSTEPer.Core;

namespace UpSTEPer.Cli;

/// <summary><c>upsteper info &lt;file&gt;</c>: prints topology, colours and tessellation statistics.</summary>
internal static class InfoCommand
{
    public static Command Create(IStepReader reader, TextWriter output)
    {
        var fileArgument = new Argument<FileInfo>("file") { Description = "STEP file (.stp/.step) to inspect." };
        var linearOption = new Option<double>("--linear-deflection")
        {
            Description = "Tessellation chord tolerance in mm.",
            DefaultValueFactory = _ => TessellationOptions.Default.LinearDeflection,
        };
        var angularOption = new Option<double>("--angular-deflection")
        {
            Description = "Tessellation angular tolerance in radians.",
            DefaultValueFactory = _ => TessellationOptions.Default.AngularDeflectionRadians,
        };

        var command = new Command("info", "Print topology, colour and mesh statistics of a STEP file.")
        {
            fileArgument,
            linearOption,
            angularOption,
        };

        command.SetAction(parseResult =>
        {
            var file = parseResult.GetValue(fileArgument)!;
            var options = new TessellationOptions(parseResult.GetValue(linearOption), parseResult.GetValue(angularOption));
            return Run(reader, output, file.FullName, options);
        });

        return command;
    }

    private static int Run(IStepReader reader, TextWriter output, string path, TessellationOptions options)
    {
        using var document = reader.Load(path);
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

        var mesh = document.Tessellate(options);
        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Mesh:      {mesh.TriangleCount} triangles, {mesh.VertexCount} vertices (linear {options.LinearDeflection} mm, angular {options.AngularDeflectionRadians} rad)"));
        return 0;
    }
}
