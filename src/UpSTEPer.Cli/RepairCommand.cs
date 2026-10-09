using System.CommandLine;
using System.Globalization;
using UpSTEPer.Core;

namespace UpSTEPer.Cli;

/// <summary>
/// <c>upsteper repair &lt;input&gt; &lt;output&gt;</c>: heals every part (sew, merge faces, fix),
/// keeps colours, prints a report per part and saves the result as STEP.
/// </summary>
internal static class RepairCommand
{
    public static Command Create(IStepReader reader, TextWriter output, TextWriter error, IProgress<OperationProgress>? progress = null)
    {
        var inputArgument = new Argument<FileInfo>("input") { Description = "STEP file to repair." };
        var outputArgument = new Argument<FileInfo>("output") { Description = "STEP file to write (overwritten)." };
        var stepsOption = new Option<string>("--steps")
        {
            Description = "Comma-separated steps: fix, sew, merge (merge faces on the same surface). Default: all.",
            DefaultValueFactory = _ => "fix,sew,merge",
        };
        var toleranceOption = Options.InvariantDouble(
            "--tolerance", "Tolerance in mm: gaps up to this size are sewn.", RepairOptions.Default.Tolerance);
        var schemaOption = new Option<StepSchema>("--schema")
        {
            Description = "STEP application protocol of the output: Ap214 (default) or Ap242.",
            DefaultValueFactory = _ => StepSchema.Ap214,
        };

        var command = new Command("repair", "Heal every part (sew gaps, merge split faces, fix), keep colours, report and save.")
        {
            inputArgument,
            outputArgument,
            stepsOption,
            toleranceOption,
            schemaOption,
        };

        command.SetAction((parseResult, cancellationToken) => Task.FromResult(CommandRunner.Run(error, () =>
        {
            var options = new RepairOptions(ParseSteps(parseResult.GetValue(stepsOption)!), parseResult.GetValue(toleranceOption));
            var input = parseResult.GetValue(inputArgument)!.FullName;
            var outputPath = parseResult.GetValue(outputArgument)!.FullName;

            using var document = reader.Load(input, progress, cancellationToken);
            var parts = document.Repair(options, progress, cancellationToken);
            WriteReport(output, parts);

            var schema = parseResult.GetValue(schemaOption);
            document.SaveStep(outputPath, schema);
            output.WriteLine($"Saved {outputPath} ({schema})");
            return 0;
        })));

        return command;
    }

    /// <summary>Parses <c>fix,sew,merge</c> (case-insensitive).</summary>
    /// <param name="text">The option value.</param>
    /// <returns>The steps.</returns>
    /// <exception cref="FormatException">Unknown or no step.</exception>
    internal static RepairSteps ParseSteps(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var steps = RepairSteps.None;
        foreach (var name in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            steps |= name.ToUpperInvariant() switch
            {
                "FIX" => RepairSteps.Fix,
                "SEW" => RepairSteps.Sew,
                "MERGE" => RepairSteps.MergeFaces,
                _ => throw new FormatException($"Unknown repair step '{name}'; use fix, sew, merge."),
            };
        }

        return steps != RepairSteps.None ? steps : throw new FormatException("--steps needs at least one of fix, sew, merge.");
    }

    private static void WriteReport(TextWriter output, IReadOnlyList<PartRepair> parts)
    {
        var nameWidth = Math.Max(4, parts.Count == 0 ? 0 : parts.Max(p => p.DisplayName.Length));
        output.WriteLine($"{"Part".PadRight(nameWidth)}  Result     Faces      Solids   Free edges  Invalid   Colour conflicts  Notes");
        foreach (var part in parts)
        {
            var result = part.Changed ? "repaired" : "unchanged";
            var notes = new List<string>();
            if (part.Failed)
            {
                notes.Add("a step failed");
            }

            if (part.Reverted)
            {
                notes.Add("a step made it worse, discarded");
            }

            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{part.DisplayName.PadRight(nameWidth)}  {result,-9}  {Change(part.FacesBefore, part.FacesAfter),-9}  {Change(part.SolidsBefore, part.SolidsAfter),-7}  {Change(part.FreeEdgesBefore, part.FreeEdgesAfter),-10}  {Change(part.InvalidBefore, part.InvalidAfter),-8}  {part.ColourConflicts,16}  {string.Join(", ", notes)}").TrimEnd());
        }

        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{parts.Count(p => p.Changed)} of {parts.Count} part(s) repaired; steps discarded as worse in {parts.Count(p => p.Reverted)}, failed in {parts.Count(p => p.Failed)}."));
    }

    private static string Change(int before, int after) =>
        before == after ? before.ToString(CultureInfo.InvariantCulture) : string.Create(CultureInfo.InvariantCulture, $"{before}->{after}");
}
