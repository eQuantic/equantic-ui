namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The lines a merge left unresolved in a text: git's conflict markers, <c>&lt;&lt;&lt;&lt;&lt;&lt;&lt;</c> where a
/// conflict opens, <c>|||||||</c> where diff3 shows the base, <c>=======</c> between the two sides and
/// <c>&gt;&gt;&gt;&gt;&gt;&gt;&gt;</c> where it closes (#1351).
/// <para>
/// Seven <c>=</c> alone on a line is also how Markdown underlines a title (a Setext heading), so it is a
/// marker only where a title's underline cannot be: inside a conflict another marker opened, or under a
/// blank line or at the start of the text, where there is no title for it to underline. A title is never
/// mistaken for a conflict, and a conflict always opens with a marker this finds.
/// </para>
/// </summary>
internal static class ConflictMarkers
{
    /// <summary>Each marker line of <paramref name="text"/>: its number, from one, and the line as it is.</summary>
    public static IReadOnlyList<(int Line, string Text)> In(string text)
    {
        var found = new List<(int Line, string Text)>();
        var lines = text.Split('\n');
        var open = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd('\r');
            if (Opens(line, "<<<<<<<") || Opens(line, "|||||||"))
            {
                found.Add((index + 1, line));
                open = true;
            }
            else if (Opens(line, ">>>>>>>"))
            {
                found.Add((index + 1, line));
                open = false;
            }
            else if (line.TrimEnd() == "=======" && (open || index == 0 || lines[index - 1].Trim().Length == 0))
            {
                found.Add((index + 1, line));
            }
        }
        return found;
    }

    /// <summary>Whether <paramref name="line"/> is <paramref name="marker"/>, alone or followed by the
    /// space git writes before the side's name: an eighth character of the same kind is not git's.</summary>
    private static bool Opens(string line, string marker) =>
        line.StartsWith(marker, StringComparison.Ordinal)
        && (line.Length == marker.Length || line[marker.Length] == ' ');
}
