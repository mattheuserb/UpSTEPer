using System.CommandLine;
using UpSTEPer.Cli;
using UpSTEPer.Occt;

// Composition root. The CLI is deliberately thin: commands translate arguments
// into calls on UpSTEPer.Core abstractions and format the result.
var reader = new OcctStepReader();

var rootCommand = new RootCommand("UpSTEPer - view, colour, repair and convert STEP models.")
{
    InfoCommand.Create(reader, Console.Out),
    RuntimeCommand.Create(Console.Out),
};

return rootCommand.Parse(args).Invoke();
