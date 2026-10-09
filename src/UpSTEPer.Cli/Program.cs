using System.CommandLine;
using UpSTEPer.Cli;
using UpSTEPer.Occt;

// Composition root. The CLI is deliberately thin: commands translate arguments
// into calls on UpSTEPer.Core abstractions and format the result.
var reader = new OcctStepReader();

// A self-overwriting progress line only makes sense in a terminal, not in a log file or pipe.
var progress = Console.IsErrorRedirected ? null : new ConsoleProgress(Console.Error);

var rootCommand = new RootCommand("UpSTEPer - view, colour, repair and convert STEP models.")
{
    InfoCommand.Create(reader, Console.Out, Console.Error, progress),
    PaintCommand.Create(reader, Console.Out, Console.Error, progress),
    ExportDxfCommand.Create(reader, Console.Out, Console.Error, progress),
    RuntimeCommand.Create(Console.Out),
};

return rootCommand.Parse(args).Invoke();
