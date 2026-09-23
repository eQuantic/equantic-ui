using eQuantic.UI.Primitives;

namespace eQuantic.UI.Code;

/// <summary>
/// Python. No braces and no block comments, so the C-family scanner does not fit: what it has
/// instead is INDENTATION that means something, triple-quoted strings that span lines, and
/// decorators.
/// </summary>
public sealed class PythonLanguage : ICodeLanguage
{
    private const int Normal = 0;
    private const int TripleDouble = 1;
    private const int TripleSingle = 2;

    public string Name => "Python";

    /// <summary>Python indents after a COLON, and has no block comment worth the name.</summary>
    public CodeLanguageRules Rules { get; } = new()
    {
        LineComment = "#",
        IndentAfter = [':', '(', '[', '{'],
        OutdentOn = [')', ']', '}'],
        IndentWidth = 4,
    };

    private static readonly HashSet<string> Keywords =
    [
        "and", "as", "assert", "async", "await", "break", "class", "continue", "def", "del",
        "elif", "else", "except", "finally", "for", "from", "global", "if", "import", "in", "is",
        "lambda", "match", "nonlocal", "not", "or", "pass", "raise", "return", "try", "while",
        "with", "yield",
    ];

    private static readonly HashSet<string> Builtins =
    [
        "bool", "bytes", "dict", "float", "frozenset", "int", "list", "object", "set", "str",
        "tuple", "type",
    ];

    private static readonly HashSet<string> Constants = ["True", "False", "None", "self", "cls"];

    public int Tokenize(string line, int state, List<CodeToken> into)
    {
        var i = 0;
        if (state != Normal)
        {
            var marker = state == TripleDouble ? "\"\"\"" : "'''";
            var close = line.IndexOf(marker, StringComparison.Ordinal);
            if (close < 0)
            {
                Add(into, 0, line.Length, CodeTokenKind.String);
                return state;
            }
            Add(into, 0, close + 3, CodeTokenKind.String);
            i = close + 3;
        }

        while (i < line.Length)
        {
            var c = line[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (c == '#')
            {
                Add(into, i, line.Length - i, CodeTokenKind.Comment);
                return Normal;
            }

            // Triple quotes first — a docstring is a string, not three empty ones.
            if (TripleAt(line, i, '"') || TripleAt(line, i, '\''))
            {
                var quote = line[i];
                var marker = quote == '"' ? "\"\"\"" : "'''";
                var close = line.IndexOf(marker, i + 3, StringComparison.Ordinal);
                if (close < 0)
                {
                    Add(into, i, line.Length - i, CodeTokenKind.String);
                    return quote == '"' ? TripleDouble : TripleSingle;
                }
                Add(into, i, close + 3 - i, CodeTokenKind.String);
                i = close + 3;
                continue;
            }

            if (c == '"' || c == '\'')
            {
                var start = i;
                // An f-string, a raw string, a bytes literal: the prefix belongs to the string.
                if (i > 0 && "fFrRbBuU".IndexOf(line[i - 1]) >= 0) start = i - 1;
                if (start < i && into.Count > 0 && into[^1].End == i) into.RemoveAt(into.Count - 1);
                var end = ScanQuoted(line, i + 1, c);
                Add(into, start, (end < 0 ? line.Length : end) - start, CodeTokenKind.String);
                i = end < 0 ? line.Length : end;
                continue;
            }

            if (char.IsDigit(c))
            {
                var end = i;
                while (end < line.Length && (char.IsLetterOrDigit(line[end]) || line[end] == '.'
                                             || line[end] == '_')) end++;
                Add(into, i, end - i, CodeTokenKind.Number);
                i = end;
                continue;
            }

            if (c == '@')
            {
                var end = i + 1;
                while (end < line.Length && (CodeDocument.IsWordChar(line[end]) || line[end] == '.')) end++;
                Add(into, i, end - i, CodeTokenKind.Attribute);
                i = end;
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < line.Length && CodeDocument.IsWordChar(line[i])) i++;
                var word = line[start..i];
                var kind = Keywords.Contains(word) ? CodeTokenKind.Keyword
                    : Constants.Contains(word) ? CodeTokenKind.Constant
                    : Builtins.Contains(word) ? CodeTokenKind.Type
                    : NextNonSpace(line, i) == '(' ? CodeTokenKind.Function
                    : char.IsUpper(word[0]) ? CodeTokenKind.Type
                    : CodeTokenKind.Plain;
                Add(into, start, i - start, kind);
                continue;
            }

            Add(into, i, 1, "()[]{},:.".IndexOf(c) >= 0
                ? CodeTokenKind.Punctuation
                : CodeTokenKind.Operator);
            i++;
        }
        return Normal;
    }

    private static bool TripleAt(string line, int i, char quote) =>
        i + 2 < line.Length && line[i] == quote && line[i + 1] == quote && line[i + 2] == quote;

    private static int ScanQuoted(string line, int from, char quote)
    {
        for (var i = from; i < line.Length; i++)
        {
            if (line[i] == '\\') { i++; continue; }
            if (line[i] == quote) return i + 1;
        }
        return -1;
    }

    private static char NextNonSpace(string line, int from)
    {
        for (var i = from; i < line.Length; i++)
            if (!char.IsWhiteSpace(line[i])) return line[i];
        return '\0';
    }

    private static void Add(List<CodeToken> into, int start, int length, CodeTokenKind kind)
    {
        if (length <= 0) return;
        if (into.Count > 0 && into[^1].Kind == kind && into[^1].End == start)
        {
            into[^1] = into[^1] with { Length = into[^1].Length + length };
            return;
        }
        into.Add(new CodeToken(start, length, kind));
    }
}
