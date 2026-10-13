using System.Globalization;
using System.Text.RegularExpressions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// One event waiting for a fold: a file under <c>docs/ledger/</c> that holds the entry as the
/// chronology of <c>docs/LEDGER.md</c> will hold it (#892).
/// <para>
/// The guard and the fold both read an entry through here, so what an entry is has one reader: a
/// list item that opens with the day and what happened, every further line a continuation of it,
/// and an issue or a pull request cited somewhere in it.
/// </para>
/// </summary>
internal sealed record LedgerEntry(string Name, string Text)
{
    /// <summary>How an entry opens: a list item whose title starts with the day it happened.</summary>
    private static readonly Regex Opening = new(@"^- \*\*(?<day>\d{4}-\d{2}-\d{2}) · \S", RegexOptions.Compiled);

    /// <summary>A line after the first: two spaces, then text, which keeps it inside the list item.
    /// An empty line, a line at the margin and a second item are each not one.</summary>
    private static readonly Regex Continuation = new(@"^  \S", RegexOptions.Compiled);

    /// <summary>A name every host writes the same way: lower-case letters, digits, dots and hyphens.
    /// Two names that differ by case alone are one file on a Mac and on Windows.</summary>
    private static readonly Regex Named = new(@"^[a-z0-9][a-z0-9.-]*\.md$", RegexOptions.Compiled);

    /// <summary>An issue or a pull request, which share one numbering.</summary>
    private static readonly Regex Evidence = new(@"#\d+", RegexOptions.Compiled);

    /// <summary>Code between backticks, where two asterisks are a glob and open nothing.</summary>
    private static readonly Regex Code = new("`[^`]*`", RegexOptions.Compiled);

    public static LedgerEntry Read(string path) =>
        new(Path.GetFileName(path), File.ReadAllText(path).ReplaceLineEndings("\n"));

    /// <summary>The lines the chronology takes: the file's, with no empty line after the last.</summary>
    public IReadOnlyList<string> Lines => Text.TrimEnd('\n').Split('\n');

    /// <summary>The day the entry states, when it states one the calendar holds.</summary>
    public DateOnly? Day =>
        Opening.Match(Lines[0]) is { Success: true } opening
        && DateOnly.TryParseExact(
            opening.Groups["day"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : null;

    /// <summary>What keeps this file from being one dated event that cites its evidence, and
    /// nothing when it is one.</summary>
    public IReadOnlyList<string> Problems
    {
        get
        {
            var lines = Lines;
            var problems = new List<string>();

            if (!Named.IsMatch(Name))
                problems.Add("its name is lower-case words joined by hyphens, ending in .md");

            if (Day is null)
                problems.Add("its first line opens `- **<year>-<month>-<day> · <what happened>`, with a day the calendar holds");
            else if (Regex.Count(Code.Replace(Text, string.Empty), @"\*\*") % 2 != 0)
                problems.Add("its title never closes: every `**` that opens bold text has one that ends it");

            for (var at = 1; at < lines.Count; at++)
            {
                if (Continuation.IsMatch(lines[at])) continue;
                problems.Add($"line {at + 1} is no continuation of the entry: every line after the first starts with two spaces and holds text, and a second event is a second file");
                break;
            }

            if (!Evidence.IsMatch(Text))
                problems.Add("it cites no issue or pull request (#n): what happened, then the link");

            return problems;
        }
    }
}
