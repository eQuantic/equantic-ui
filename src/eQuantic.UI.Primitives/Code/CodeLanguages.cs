namespace eQuantic.UI.Primitives;

/// <summary>C# — the language the SDK itself is written in, and the one every snippet in its own
/// documentation shows.</summary>
public sealed class CSharpLanguage : CurlyBraceLanguage
{
    public override string Name => "C#";

    protected override bool HasVerbatimStrings => true;
    protected override bool HasBracketAttributes => true;

    public override CodeLanguageRules Rules { get; } = new()
    {
        LineComment = "//",
        BlockComment = ("/*", "*/"),
    };

    protected override IReadOnlySet<string> Keywords { get; } = new HashSet<string>
    {
        "abstract", "as", "async", "await", "base", "break", "case", "catch", "checked", "class",
        "const", "continue", "default", "delegate", "do", "else", "enum", "event", "explicit",
        "extern", "file", "finally", "fixed", "for", "foreach", "get", "global", "goto", "if",
        "implicit", "in", "init", "interface", "internal", "is", "lock", "namespace", "new", "not",
        "operator", "out", "override", "params", "partial", "private", "protected", "public",
        "readonly", "record", "ref", "required", "return", "sealed", "set", "sizeof", "stackalloc",
        "static", "struct", "switch", "this", "throw", "try", "typeof", "unchecked", "unsafe",
        "using", "value", "virtual", "volatile", "when", "where", "while", "with", "yield",
    };

    protected override IReadOnlySet<string> TypeWords { get; } = new HashSet<string>
    {
        "bool", "byte", "char", "decimal", "double", "dynamic", "float", "int", "long", "nint",
        "nuint", "object", "sbyte", "short", "string", "uint", "ulong", "ushort", "var", "void",
    };

    protected override IReadOnlySet<string> ConstantWords { get; } = new HashSet<string>
    {
        "true", "false", "null", "default",
    };
}

/// <summary>TypeScript, and JavaScript with it — the transpiled twin of everything above.</summary>
public sealed class TypeScriptLanguage : CurlyBraceLanguage
{
    public override string Name => "TypeScript";

    protected override bool HasTemplateStrings => true;
    protected override bool HasAtDecorators => true;

    public override CodeLanguageRules Rules { get; } = new()
    {
        LineComment = "//",
        BlockComment = ("/*", "*/"),
        IndentWidth = 2,
    };

    protected override IReadOnlySet<string> Keywords { get; } = new HashSet<string>
    {
        "abstract", "any", "as", "async", "await", "break", "case", "catch", "class", "const",
        "constructor", "continue", "debugger", "declare", "default", "delete", "do", "else",
        "enum", "export", "extends", "finally", "for", "from", "function", "get", "if",
        "implements", "import", "in", "infer", "instanceof", "interface", "is", "keyof", "let",
        "namespace", "new", "of", "private", "protected", "public", "readonly", "return",
        "satisfies", "set", "static", "super", "switch", "this", "throw", "try", "type", "typeof",
        "var", "while", "yield",
    };

    protected override IReadOnlySet<string> TypeWords { get; } = new HashSet<string>
    {
        "bigint", "boolean", "never", "number", "object", "string", "symbol", "unknown", "void",
    };

    protected override IReadOnlySet<string> ConstantWords { get; } = new HashSet<string>
    {
        "true", "false", "null", "undefined", "NaN", "Infinity",
    };
}

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

/// <summary>No language at all: one plain span per line. The fallback, and the honest answer for
/// a log, a licence or a paste from somewhere unknown.</summary>
public sealed class PlainTextLanguage : ICodeLanguage
{
    public string Name => "Text";

    public int Tokenize(string line, int state, List<CodeToken> into)
    {
        if (line.Length > 0) into.Add(new CodeToken(0, line.Length, CodeTokenKind.Plain));
        return 0;
    }
}

/// <summary>
/// The languages this SDK ships, by name. A registry rather than an enum because an app may bring
/// its own: <c>CodeLanguages.Register(new SqlLanguage())</c> and the editor colours it with no
/// change here.
/// </summary>
public static class CodeLanguages
{
    public static readonly ICodeLanguage CSharp = new CSharpLanguage();
    public static readonly ICodeLanguage TypeScript = new TypeScriptLanguage();
    public static readonly ICodeLanguage Python = new PythonLanguage();
    public static readonly ICodeLanguage Json = new JsonLanguage();
    public static readonly ICodeLanguage Xml = new XmlLanguage();
    public static readonly ICodeLanguage PlainText = new PlainTextLanguage();

    private static readonly Dictionary<string, ICodeLanguage> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["c#"] = CSharp, ["csharp"] = CSharp, ["cs"] = CSharp,
        ["typescript"] = TypeScript, ["ts"] = TypeScript,
        ["javascript"] = TypeScript, ["js"] = TypeScript, ["tsx"] = TypeScript, ["jsx"] = TypeScript,
        ["python"] = Python, ["py"] = Python,
        ["json"] = Json,
        ["xml"] = Xml, ["csproj"] = Xml, ["html"] = Xml, ["plist"] = Xml,
        ["text"] = PlainText, ["txt"] = PlainText, ["plain"] = PlainText,
    };

    /// <summary>Adds (or replaces) a language under a name — an app's own dialect joins here.</summary>
    public static void Register(string name, ICodeLanguage language) => Known[name] = language;

    /// <summary>The language for a name or a file extension; plain text when nothing matches, which
    /// is the answer that renders rather than the one that throws.</summary>
    public static ICodeLanguage For(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return PlainText;
        var key = name.TrimStart('.');
        return Known.TryGetValue(key, out var language) ? language : PlainText;
    }
}
