using eQuantic.UI.Primitives;

namespace eQuantic.UI.Code;

/// <summary>XML, and the .csproj and .plist that are XML wearing another extension.</summary>
public sealed class XmlLanguage : ICodeLanguage
{
    private const int Normal = 0;
    private const int InComment = 1;

    public string Name => "XML";

    public CodeLanguageRules Rules { get; } = new()
    {
        BlockComment = ("<!--", "-->"),
        Brackets = [('<', '>'), ('(', ')'), ('[', ']')],
        IndentAfter = ['>'],
        OutdentOn = ['<'],
        IndentWidth = 2,
    };

    public int Tokenize(string line, int state, List<CodeToken> into)
    {
        var i = 0;
        if (state == InComment)
        {
            var close = line.IndexOf("-->", StringComparison.Ordinal);
            if (close < 0)
            {
                into.Add(new CodeToken(0, line.Length, CodeTokenKind.Comment));
                return InComment;
            }
            into.Add(new CodeToken(0, close + 3, CodeTokenKind.Comment));
            i = close + 3;
        }

        while (i < line.Length)
        {
            var c = line[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (c == '<')
            {
                if (line[i..].StartsWith("<!--", StringComparison.Ordinal))
                {
                    var close = line.IndexOf("-->", i, StringComparison.Ordinal);
                    if (close < 0)
                    {
                        into.Add(new CodeToken(i, line.Length - i, CodeTokenKind.Comment));
                        return InComment;
                    }
                    into.Add(new CodeToken(i, close + 3 - i, CodeTokenKind.Comment));
                    i = close + 3;
                    continue;
                }

                // The tag's punctuation, then its NAME.
                var nameStart = i + 1;
                while (nameStart < line.Length && (line[nameStart] == '/' || line[nameStart] == '?'
                                                   || line[nameStart] == '!')) nameStart++;
                into.Add(new CodeToken(i, nameStart - i, CodeTokenKind.Punctuation));
                var nameEnd = nameStart;
                while (nameEnd < line.Length && (CodeDocument.IsWordChar(line[nameEnd])
                                                 || line[nameEnd] == '-' || line[nameEnd] == ':')) nameEnd++;
                if (nameEnd > nameStart)
                    into.Add(new CodeToken(nameStart, nameEnd - nameStart, CodeTokenKind.Keyword));
                i = nameEnd;
                continue;
            }

            if (c == '"' || c == '\'')
            {
                var end = i + 1;
                while (end < line.Length && line[end] != c) end++;
                if (end < line.Length) end++;
                into.Add(new CodeToken(i, end - i, CodeTokenKind.String));
                i = end;
                continue;
            }

            if (CodeDocument.IsWordChar(c))
            {
                var end = i;
                while (end < line.Length && (CodeDocument.IsWordChar(line[end]) || line[end] == '-'
                                             || line[end] == ':')) end++;
                // A word followed by '=' is an ATTRIBUTE name; anything else is text.
                var kind = NextNonSpace(line, end) == '='
                    ? CodeTokenKind.Property
                    : CodeTokenKind.Plain;
                into.Add(new CodeToken(i, end - i, kind));
                i = end;
                continue;
            }

            into.Add(new CodeToken(i, 1,
                c is '>' or '/' or '=' ? CodeTokenKind.Punctuation : CodeTokenKind.Plain));
            i++;
        }
        return Normal;
    }

    private static char NextNonSpace(string line, int from)
    {
        for (var i = from; i < line.Length; i++)
            if (!char.IsWhiteSpace(line[i])) return line[i];
        return '\0';
    }
}
