using System.CommandLine;
using System.Globalization;
using UpSTEPer.Core;

namespace UpSTEPer.Cli;

/// <summary>
/// <c>upsteper check &lt;file&gt;</c>: lists typical defects per part definition. Exit code 0 when
/// nothing needs repair, <see cref="ProblemsFound"/> otherwise; distinct from 1 (invalid
/// arguments, System.CommandLine) and <see cref="CommandRunner.InputError"/> (unreadable file).
/// </summary>
internal static class CheckCommand
{
    /// <summary>Exit code when at least one part has problems.</summary>
    public const int ProblemsFound = 3;

    public static Command Create(IStepReader reader, TextWriter output, TextWriter error, IProgress<OperationProgress>? progress = null)
    {
        var fileArgument = new Argument<FileInfo>("file") { Description = "STEP file (.stp/.step) to check." };
        var toleranceOption = Options.InvariantDouble(
            "--tolerance", "Tolerance in mm for recognising canonical geometry and small edges.", 0.01);

        var command = new Command(
            "check",
            "List defects per part: gaps, invalid shapes, splines that are canonical, mergeable faces, small edges. Exit code 3 if any are found.")
        {
            fileArgument,
            toleranceOption,
        };

        command.SetAction((parseResult, cancellationToken) => Task.FromResult(CommandRunner.Run(error, () =>
        {
            var path = parseResult.GetValue(fileArgument)!.FullName;
            using var document = reader.Load(path, progress, cancellationToken);
            return Run(document, output, parseResult.GetValue(toleranceOption));
        })));

        return command;
    }

    private static int Run(IStepDocument document, TextWriter output, double tolerance)
    {
        var parts = document.Check(tolerance);
        var nameWidth = Math.Max(4, parts.Count == 0 ? 0 : parts.Max(p => p.DisplayName.Length));

        output.WriteLine($"{"Part".PadRight(nameWidth)}  Solids  Faces  Free edges  Invalid  Splines (canonical)  Mergeable  Small edges  Errors");
        foreach (var part in parts)
        {
            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{part.DisplayName.PadRight(nameWidth)}  {part.SolidCount,6}  {part.FaceCount,5}  {part.FreeEdges,10}  {part.InvalidShapes,7}  {$"{part.SplineFaces} ({part.CanonicalFaces})",19}  {part.MergeableFaces,9}  {part.SmallEdges,11}  {part.FailedChecks,6}"));
        }

        var withProblems = parts.Count(p => p.HasProblems);
        output.WriteLine(withProblems == 0
            ? $"No problems found in {parts.Count} part(s)."
            : $"{withProblems} of {parts.Count} part(s) have problems.");
        return withProblems == 0 ? 0 : ProblemsFound;
    }
}
