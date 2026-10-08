using System.CommandLine;
using UpSTEPer.Core;

namespace UpSTEPer.Cli;

/// <summary>
/// <c>upsteper paint &lt;input&gt; &lt;output&gt; --face 80=#FF0000 --body 0=#3366CC</c>:
/// assigns or removes face and body colours and writes a new STEP file.
/// </summary>
internal static class PaintCommand
{
    public static Command Create(IStepReader reader, TextWriter output, TextWriter error)
    {
        var inputArgument = new Argument<FileInfo>("input") { Description = "STEP file to read." };
        var outputArgument = new Argument<FileInfo>("output") { Description = "STEP file to write (overwritten)." };
        var faceOption = new Option<string[]>("--face")
        {
            Description = "Face colour, <faceId>=<#RRGGBB|none>. Repeatable.",
        };
        var bodyOption = new Option<string[]>("--body")
        {
            Description = "Body colour, <bodyId>=<#RRGGBB|none>. Applies to faces without their own colour. Repeatable.",
        };

        var command = new Command("paint", "Assign face and body colours and save as STEP.")
        {
            inputArgument,
            outputArgument,
            faceOption,
            bodyOption,
        };

        command.SetAction(parseResult => CommandRunner.Run(error, () =>
        {
            // Body colours first, so face colours given in the same call are applied on top.
            var assignments = (parseResult.GetValue(bodyOption) ?? [])
                .Select(text => ColorAssignment.Parse(text, ColorTargetKind.Body))
                .Concat((parseResult.GetValue(faceOption) ?? []).Select(text => ColorAssignment.Parse(text, ColorTargetKind.Face)))
                .ToList();

            if (assignments.Count == 0)
            {
                throw new FormatException("Nothing to do: give at least one --face or --body assignment.");
            }

            return Run(reader, output, parseResult.GetValue(inputArgument)!.FullName, parseResult.GetValue(outputArgument)!.FullName, assignments);
        }));

        return command;
    }

    private static int Run(IStepReader reader, TextWriter output, string input, string outputPath, IReadOnlyList<ColorAssignment> assignments)
    {
        using var document = reader.Load(input);
        foreach (var assignment in assignments)
        {
            document.SetColor(assignment.Target, assignment.Color);
            output.WriteLine($"Set {assignment}");
        }

        document.SaveStep(outputPath);
        output.WriteLine($"Saved {outputPath}");
        return 0;
    }
}
