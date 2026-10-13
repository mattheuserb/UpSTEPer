using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UpSTEPer.Cli;
using UpSTEPer.Core;
using UpSTEPer.Occt;

// Composition root. The CLI is deliberately thin: commands translate arguments
// into calls on UpSTEPer.Core abstractions and format the result.
using var services = CliServices.Build(CliServices.IsVerbose(args));
using var occtMessages = OcctMessageRouter.TryStart(services.GetRequiredService<ILoggerFactory>().CreateLogger("OCCT"));
var reader = services.GetRequiredService<IStepReader>();

// A self-overwriting progress line only makes sense in a terminal, not in a log file or pipe.
var progress = Console.IsErrorRedirected ? null : new ConsoleProgress(Console.Error);

var rootCommand = new RootCommand("UpSTEPer - view, colour, repair and convert STEP models.")
{
    InfoCommand.Create(reader, Console.Out, Console.Error, progress),
    PaintCommand.Create(reader, Console.Out, Console.Error, progress),
    CheckCommand.Create(reader, Console.Out, Console.Error, progress),
    RepairCommand.Create(reader, Console.Out, Console.Error, progress),
    ExportDxfCommand.Create(reader, Console.Out, Console.Error, progress),
    RuntimeCommand.Create(Console.Out),
};
rootCommand.Options.Add(new Option<bool>(CliServices.VerboseOption)
{
    Description = "Log diagnostic details to stderr: load times and Open CASCADE messages about the file.",
    Recursive = true,
});

return rootCommand.Parse(args).Invoke();
