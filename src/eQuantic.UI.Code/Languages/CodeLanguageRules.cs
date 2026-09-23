namespace eQuantic.UI.Code;

/// <summary>
/// What a language needs beyond colours, and what every editor behaviour is built from: how it
/// comments, which brackets pair, and what opens a new indentation level. An IDE author fills
/// these in for a new dialect and gets ⌘/, auto-indent, auto-closing and bracket matching without
/// writing any of them.
/// </summary>
public sealed record CodeLanguageRules
{
    public static readonly CodeLanguageRules Default = new();

    /// <summary>What starts a comment to end of line — <c>//</c>, <c>#</c>. Null = the language
    /// has none, and the comment command does nothing rather than corrupting the file.</summary>
    public string? LineComment { get; init; }

    /// <summary>The pair that wraps a block comment.</summary>
    public (string Open, string Close)? BlockComment { get; init; }

    /// <summary>Pairs that auto-close, match, and jump — the same table serves all three.</summary>
    public IReadOnlyList<(char Open, char Close)> Brackets { get; init; } =
        [('(', ')'), ('[', ']'), ('{', '}')];

    /// <summary>Quotes that auto-close around a caret (and never inside a word).</summary>
    public IReadOnlyList<char> Quotes { get; init; } = ['"', '\''];

    /// <summary>Characters that, at the end of a line, mean the NEXT line indents one level.</summary>
    public IReadOnlyList<char> IndentAfter { get; init; } = ['{', '(', '['];

    /// <summary>Characters that, typed at the head of a line, mean this line un-indents — a
    /// closing brace snapping back as you type it.</summary>
    public IReadOnlyList<char> OutdentOn { get; init; } = ['}', ')', ']'];

    /// <summary>How wide one indentation step is, in spaces.</summary>
    public int IndentWidth { get; init; } = 4;

    /// <summary>Whether Tab inserts spaces (the default) or a tab character.</summary>
    public bool InsertSpaces { get; init; } = true;
}
