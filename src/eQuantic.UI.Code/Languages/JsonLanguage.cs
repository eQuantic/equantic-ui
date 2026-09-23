using eQuantic.UI.Primitives;

namespace eQuantic.UI.Code;

/// <summary>
/// JSON. The one thing worth colouring differently from a generic C-family scan is the KEY: the
/// string before a colon is a property, and a reader scans configuration by its keys.
/// </summary>
public sealed class JsonLanguage : ICodeLanguage
{
    public string Name => "JSON";

    /// <summary>JSON has no comments — the command must do NOTHING rather than write a line the
    /// parser will reject.</summary>
    public CodeLanguageRules Rules { get; } = new() { IndentWidth = 2, Quotes = ['"'] };

    public int Tokenize(string line, int state, List<CodeToken> into)
    {
        var i = 0;
        while (i < line.Length)
        {
            var c = line[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (c == '"')
            {
                var end = i + 1;
                while (end < line.Length)
                {
                    if (line[end] == '\\') { end += 2; continue; }
                    if (line[end] == '"') { end++; break; }
                    end++;
                }
                var isKey = NextNonSpace(line, end) == ':';
                into.Add(new CodeToken(i, Math.Min(end, line.Length) - i,
                    isKey ? CodeTokenKind.Property : CodeTokenKind.String));
                i = Math.Min(end, line.Length);
                continue;
            }

            if (char.IsDigit(c) || (c == '-' && i + 1 < line.Length && char.IsDigit(line[i + 1])))
            {
                var end = i + 1;
                while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '.'
                                             || line[end] == 'e' || line[end] == 'E'
                                             || line[end] == '+' || line[end] == '-')) end++;
                into.Add(new CodeToken(i, end - i, CodeTokenKind.Number));
                i = end;
                continue;
            }

            if (char.IsLetter(c))
            {
                var end = i;
                while (end < line.Length && char.IsLetter(line[end])) end++;
                into.Add(new CodeToken(i, end - i, CodeTokenKind.Constant));   // true / false / null
                i = end;
                continue;
            }

            into.Add(new CodeToken(i, 1, CodeTokenKind.Punctuation));
            i++;
        }
        return 0;
    }

    private static char NextNonSpace(string line, int from)
    {
        for (var i = from; i < line.Length; i++)
            if (!char.IsWhiteSpace(line[i])) return line[i];
        return '\0';
    }
}
