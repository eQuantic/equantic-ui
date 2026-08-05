namespace eQuantic.UI.Primitives;

/// <summary>
/// The shared scanner for the C-family: C#, TypeScript, JavaScript. They differ in their keyword
/// sets and in which multi-line string shapes they have, and in nothing else that matters to a
/// colouriser — so the SCANNING lives here once and each language brings a table.
/// <para>
/// The carried state is what makes a construct that spans lines work: a block comment opened on
/// one line, a C# verbatim string, a JS template literal. Anything else re-starts fresh on every
/// line, which is what keeps re-colouring a keystroke cheap.
/// </para>
/// </summary>
public abstract class CurlyBraceLanguage : ICodeLanguage
{
    /// <summary>Nothing carried — the line starts clean.</summary>
    protected const int StateNormal = 0;

    /// <summary>Inside a <c>/* … */</c> that opened on an earlier line.</summary>
    protected const int StateBlockComment = 1;

    /// <summary>Inside a multi-line string: C#'s <c>@"…"</c>, JS's <c>`…`</c>.</summary>
    protected const int StateMultilineString = 2;

    public abstract string Name { get; }

    /// <inheritdoc />
    public virtual CodeLanguageRules Rules { get; } = CodeLanguageRules.Default;

    /// <summary>The reserved words, exactly as written.</summary>
    protected abstract IReadOnlySet<string> Keywords { get; }

    /// <summary>Words that name a TYPE (the built-ins; declared types are caught by convention).</summary>
    protected abstract IReadOnlySet<string> TypeWords { get; }

    /// <summary>Words that ARE a value: true, false, null, undefined.</summary>
    protected abstract IReadOnlySet<string> ConstantWords { get; }

    /// <summary>Whether <c>@"…"</c> opens a string that runs to the closing quote (C# verbatim).</summary>
    protected virtual bool HasVerbatimStrings => false;

    /// <summary>Whether <c>`…`</c> opens a string that spans lines (JS template literal).</summary>
    protected virtual bool HasTemplateStrings => false;

    /// <summary>Whether <c>[Attribute]</c> at the head of a line is an attribute (C#).</summary>
    protected virtual bool HasBracketAttributes => false;

    /// <summary>Whether <c>@decorator</c> marks a decorator (TypeScript).</summary>
    protected virtual bool HasAtDecorators => false;

    public int Tokenize(string line, int state, List<CodeToken> into)
    {
        var i = 0;

        // A construct carried over from the line above owns the head of this line.
        if (state == StateBlockComment)
        {
            var close = line.IndexOf("*/", StringComparison.Ordinal);
            if (close < 0)
            {
                Add(into, 0, line.Length, CodeTokenKind.Comment);
                return StateBlockComment;
            }
            Add(into, 0, close + 2, CodeTokenKind.Comment);
            i = close + 2;
        }
        else if (state == StateMultilineString)
        {
            var end = CloseMultilineString(line);
            if (end < 0)
            {
                Add(into, 0, line.Length, CodeTokenKind.String);
                return StateMultilineString;
            }
            Add(into, 0, end, CodeTokenKind.String);
            i = end;
        }

        while (i < line.Length)
        {
            var c = line[i];

            if (char.IsWhiteSpace(c)) { i++; continue; }

            // Comments first: everything else could be inside one.
            if (c == '/' && i + 1 < line.Length)
            {
                if (line[i + 1] == '/')
                {
                    Add(into, i, line.Length - i, CodeTokenKind.Comment);
                    return StateNormal;
                }
                if (line[i + 1] == '*')
                {
                    var close = line.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (close < 0)
                    {
                        Add(into, i, line.Length - i, CodeTokenKind.Comment);
                        return StateBlockComment;
                    }
                    Add(into, i, close + 2 - i, CodeTokenKind.Comment);
                    i = close + 2;
                    continue;
                }
            }

            // Strings and characters.
            if (HasVerbatimStrings && c == '@' && i + 1 < line.Length && line[i + 1] == '"')
            {
                var end = ScanVerbatim(line, i + 2);
                if (end < 0)
                {
                    Add(into, i, line.Length - i, CodeTokenKind.String);
                    return StateMultilineString;
                }
                Add(into, i, end - i, CodeTokenKind.String);
                i = end;
                continue;
            }
            if (HasTemplateStrings && c == '`')
            {
                var end = ScanQuoted(line, i + 1, '`');
                if (end < 0)
                {
                    Add(into, i, line.Length - i, CodeTokenKind.String);
                    return StateMultilineString;
                }
                Add(into, i, end - i, CodeTokenKind.String);
                i = end;
                continue;
            }
            if (c == '"' || c == '\'')
            {
                // An interpolated C# string opens with $" — the $ belongs to the string.
                var start = i > 0 && line[i - 1] == '$' ? i - 1 : i;
                if (start < i && into.Count > 0 && into[^1].Start == start) into.RemoveAt(into.Count - 1);
                var end = ScanQuoted(line, i + 1, c);
                var length = (end < 0 ? line.Length : end) - start;
                Add(into, start, length, CodeTokenKind.String);
                i = end < 0 ? line.Length : end;
                continue;
            }

            // Numbers: 42, 3.14, 0xFF, 1_000, 1e-9, 10f.
            if (char.IsDigit(c))
            {
                var end = ScanNumber(line, i);
                Add(into, i, end - i, CodeTokenKind.Number);
                i = end;
                continue;
            }

            // Words.
            if (char.IsLetter(c) || c == '_' || (HasAtDecorators && c == '@'))
            {
                var start = i;
                if (line[i] == '@') i++;
                while (i < line.Length && CodeDocument.IsWordChar(line[i])) i++;
                var word = line[start..i];
                Add(into, start, i - start, WordKind(word, line, i));
                continue;
            }

            // A C# attribute is a bracketed word at the head of a line: [Fact], [Page("/x")].
            if (HasBracketAttributes && c == '[' && IsLineHead(line, i))
            {
                var close = line.IndexOf(']', i);
                if (close > 0)
                {
                    Add(into, i, close + 1 - i, CodeTokenKind.Attribute);
                    i = close + 1;
                    continue;
                }
            }

            Add(into, i, 1, Punctuation.Contains(c) ? CodeTokenKind.Punctuation : CodeTokenKind.Operator);
            i++;
        }

        return StateNormal;
    }

    /// <summary>What a bare word means here. Overridable for a language with its own habits.</summary>
    protected virtual CodeTokenKind WordKind(string word, string line, int afterIndex)
    {
        if (word.Length > 1 && word[0] == '@') return CodeTokenKind.Attribute;
        if (Keywords.Contains(word)) return CodeTokenKind.Keyword;
        if (ConstantWords.Contains(word)) return CodeTokenKind.Constant;
        if (TypeWords.Contains(word)) return CodeTokenKind.Type;

        // A word followed by '(' is being CALLED; a word that starts upper-case is a type, which
        // is a convention rather than a rule — and a convention is all a colouriser needs.
        var next = NextNonSpace(line, afterIndex);
        if (next == '(') return CodeTokenKind.Function;
        if (word.Length > 0 && char.IsUpper(word[0])) return CodeTokenKind.Type;
        return CodeTokenKind.Plain;
    }

    /// <summary>Where the carried multi-line string ends on this line, or -1 if it does not.</summary>
    protected virtual int CloseMultilineString(string line)
    {
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '`' && HasTemplateStrings) return i + 1;
            if (line[i] != '"') continue;
            // A doubled quote inside a verbatim string is an escaped quote, not the end.
            if (i + 1 < line.Length && line[i + 1] == '"') { i++; continue; }
            return i + 1;
        }
        return -1;
    }

    private static readonly HashSet<char> Punctuation = ['(', ')', '[', ']', '{', '}', ',', ';', '.', ':'];

    private static void Add(List<CodeToken> into, int start, int length, CodeTokenKind kind)
    {
        if (length <= 0) return;
        // Runs of the same kind merge — fewer spans means fewer nodes on screen.
        if (into.Count > 0 && into[^1].Kind == kind && into[^1].End == start)
        {
            into[^1] = into[^1] with { Length = into[^1].Length + length };
            return;
        }
        into.Add(new CodeToken(start, length, kind));
    }

    /// <summary>Scans to the closing quote, honouring backslash escapes. -1 = the line ended first.</summary>
    protected static int ScanQuoted(string line, int from, char quote)
    {
        for (var i = from; i < line.Length; i++)
        {
            if (line[i] == '\\') { i++; continue; }
            if (line[i] == quote) return i + 1;
        }
        return -1;
    }

    /// <summary>Scans a verbatim string, where the only escape is a doubled quote.</summary>
    protected static int ScanVerbatim(string line, int from)
    {
        for (var i = from; i < line.Length; i++)
        {
            if (line[i] != '"') continue;
            if (i + 1 < line.Length && line[i + 1] == '"') { i++; continue; }
            return i + 1;
        }
        return -1;
    }

    protected static int ScanNumber(string line, int from)
    {
        var i = from;
        if (line[i] == '0' && i + 1 < line.Length && (line[i + 1] == 'x' || line[i + 1] == 'X' ||
                                                      line[i + 1] == 'b' || line[i + 1] == 'B'))
        {
            i += 2;
            while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_')) i++;
            return i;
        }
        while (i < line.Length && (char.IsDigit(line[i]) || line[i] == '_')) i++;
        if (i < line.Length && line[i] == '.' && i + 1 < line.Length && char.IsDigit(line[i + 1]))
        {
            i++;
            while (i < line.Length && (char.IsDigit(line[i]) || line[i] == '_')) i++;
        }
        if (i < line.Length && (line[i] == 'e' || line[i] == 'E'))
        {
            var exponent = i + 1;
            if (exponent < line.Length && (line[exponent] == '+' || line[exponent] == '-')) exponent++;
            if (exponent < line.Length && char.IsDigit(line[exponent]))
            {
                i = exponent;
                while (i < line.Length && char.IsDigit(line[i])) i++;
            }
        }
        // A numeric suffix: 10f, 3.14m, 42L, 1u.
        while (i < line.Length && char.IsLetter(line[i])) i++;
        return i;
    }

    protected static char NextNonSpace(string line, int from)
    {
        for (var i = from; i < line.Length; i++)
            if (!char.IsWhiteSpace(line[i])) return line[i];
        return '\0';
    }

    private static bool IsLineHead(string line, int index)
    {
        for (var i = 0; i < index; i++)
            if (!char.IsWhiteSpace(line[i])) return false;
        return true;
    }
}
