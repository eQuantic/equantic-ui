using eQuantic.UI.Code;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Sheet;

/// <summary>
/// The language a cell's entry is typed in: plain text that pairs nothing and indents nothing. The code
/// engine's plain text closes a bracket or a quote as it is typed and carries a line's indentation onto
/// the next, which a cell must never do: `(` is a character a person typed, as Excel keeps it. The
/// formula package of slice 7 brings a language of its own for an entry that begins with `=`.
/// </summary>
public sealed class SheetEntryLanguage : ICodeLanguage
{
    private static readonly CodeLanguageRules NothingPaired = new()
    {
        Brackets = [],
        Quotes = [],
        IndentAfter = [],
        OutdentOn = [],
    };

    public string Name => "Cell";

    public int Tokenize(string line, int state, List<CodeToken> into)
    {
        if (line.Length > 0) into.Add(new CodeToken(0, line.Length, CodeTokenKind.Plain));
        return 0;
    }

    public CodeLanguageRules Rules => NothingPaired;
}
