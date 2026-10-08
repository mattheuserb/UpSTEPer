using System.CommandLine;
using UpSTEPer.Core;
using UpSTEPer.Core.Drawing;
using UpSTEPer.Export.Dxf;

namespace UpSTEPer.Cli;

/// <summary>
/// <c>upsteper export-dxf &lt;input&gt; &lt;output.dxf&gt; [--views front,top,left,iso]</c>:
/// 2D views with hidden-line removal, arranged in first-angle projection.
/// </summary>
internal static class ExportDxfCommand
{
    public static Command Create(IStepReader reader, TextWriter output, TextWriter error)
    {
        var inputArgument = new Argument<FileInfo>("input") { Description = "STEP file to read." };
        var outputArgument = new Argument<FileInfo>("output") { Description = "DXF file to write (overwritten)." };
        var viewsOption = new Option<string>("--views")
        {
            Description = "Comma-separated views: front, top, left, right, back, bottom, iso.",
            DefaultValueFactory = _ => string.Join(',', DrawingBuilder.StandardViews),
        };
        var noHiddenOption = new Option<bool>("--no-hidden") { Description = "Omit hidden (dashed) lines." };
        var tangentOption = new Option<bool>("--tangent") { Description = "Include smooth edges between faces." };
        var gapOption = new Option<double?>("--gap")
        {
            Description = "Space between views in mm (default: 20 % of the largest view, at least 10).",
            CustomParser = result => (double?)Options.ParseInvariantDouble(result),
        };

        var command = new Command("export-dxf", "Export 2D views (front/top/left/iso by default) as DXF.")
        {
            inputArgument,
            outputArgument,
            viewsOption,
            noHiddenOption,
            tangentOption,
            gapOption,
        };

        command.SetAction(parseResult => CommandRunner.Run(error, () =>
        {
            var views = ParseViews(parseResult.GetValue(viewsOption)!);
            var options = ProjectionOptions.Default with
            {
                IncludeHidden = !parseResult.GetValue(noHiddenOption),
                IncludeTangent = parseResult.GetValue(tangentOption),
            };
            var input = parseResult.GetValue(inputArgument)!.FullName;
            var outputPath = parseResult.GetValue(outputArgument)!.FullName;

            using var document = reader.Load(input);
            var arranged = DrawingBuilder.Build(document, views, options, parseResult.GetValue(gapOption));
            DxfExporter.Write(outputPath, arranged);

            foreach (var view in arranged)
            {
                output.WriteLine($"{view.Direction,-6} {view.Curves.Count} curves");
            }

            output.WriteLine($"Saved {outputPath}");
            return 0;
        }));

        return command;
    }

    /// <summary>Parses <c>front,top,iso</c> (case-insensitive, no duplicates).</summary>
    /// <param name="text">The option value.</param>
    /// <returns>The view directions in the given order.</returns>
    /// <exception cref="FormatException">Unknown or duplicate view, or none.</exception>
    internal static IReadOnlyList<ViewDirection> ParseViews(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var views = new List<ViewDirection>();
        foreach (var name in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<ViewDirection>(name, ignoreCase: true, out var view) || !Enum.IsDefined(view) || int.TryParse(name, out _))
            {
                throw new FormatException($"Unknown view '{name}'. Use front, top, left, right, back, bottom or iso.");
            }

            if (views.Contains(view))
            {
                throw new FormatException($"View '{name}' is listed twice.");
            }

            views.Add(view);
        }

        return views.Count > 0 ? views : throw new FormatException("Give at least one view.");
    }
}
