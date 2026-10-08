using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;

namespace UpSTEPer.Cli;

/// <summary>Option factories shared by commands.</summary>
internal static class Options
{
    /// <summary>
    /// Creates a <see cref="double"/> option that always parses with the invariant culture, so
    /// <c>0.05</c> means 0.05 on every machine (a German locale would otherwise read it as 5).
    /// </summary>
    public static Option<double> InvariantDouble(string name, string description, double defaultValue) =>
        new(name)
        {
            Description = description,
            DefaultValueFactory = _ => defaultValue,
            CustomParser = ParseInvariantDouble,
        };

    /// <summary>Parses the single token of <paramref name="result"/> as an invariant-culture number.</summary>
    /// <param name="result">The argument result.</param>
    /// <returns>The value; on failure an error is added to <paramref name="result"/> and 0 is returned.</returns>
    public static double ParseInvariantDouble(ArgumentResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var token = result.Tokens.Count == 1 ? result.Tokens[0].Value : string.Empty;
        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        result.AddError($"'{token}' is not a number. Use a dot as decimal separator, e.g. 0.05.");
        return default;
    }
}
