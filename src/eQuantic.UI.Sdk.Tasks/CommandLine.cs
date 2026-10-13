using System.Text;

namespace eQuantic.UI.Sdk.Tasks;

/// <summary>
/// One command line from a list of arguments, written so that the program started with it reads back
/// exactly that list. It is the convention every program on Windows splits its command line by, and the
/// one .NET applies to a command line on macOS and Linux before handing the arguments to the program,
/// so the same text reaches every system whole: what .NET writes itself for a
/// <c>ProcessStartInfo.ArgumentList</c>, which a task built for every MSBuild cannot use.
/// </summary>
internal static class CommandLine
{
    /// <summary>The command line that hands a program <paramref name="arguments"/>, in order.</summary>
    public static string Of(IEnumerable<string> arguments)
    {
        var line = new StringBuilder();
        foreach (var argument in arguments)
        {
            if (line.Length > 0) line.Append(' ');
            Append(line, argument);
        }
        return line.ToString();
    }

    /// <summary>
    /// <paramref name="argument"/> as it is when nothing in it splits or quotes, and in quotes
    /// otherwise: a quote inside is escaped, and so is each backslash that ends up before a quote,
    /// the only place a backslash means anything.
    /// </summary>
    private static void Append(StringBuilder line, string argument)
    {
        if (argument.Length > 0 && !argument.Any(c => char.IsWhiteSpace(c) || c == '"'))
        {
            line.Append(argument);
            return;
        }

        line.Append('"');
        for (var at = 0; at < argument.Length; at++)
        {
            var backslashes = 0;
            while (at < argument.Length && argument[at] == '\\')
            {
                backslashes++;
                at++;
            }

            if (at == argument.Length)
            {
                // The closing quote follows: every backslash before it is doubled.
                line.Append('\\', backslashes * 2);
                break;
            }
            if (argument[at] == '"')
            {
                // A quote inside: the backslashes before it doubled, and one more for the quote.
                line.Append('\\', backslashes * 2 + 1).Append('"');
                continue;
            }
            line.Append('\\', backslashes).Append(argument[at]);
        }
        line.Append('"');
    }
}
