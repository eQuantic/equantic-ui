using eQuantic.UI.Primitives;

namespace eQuantic.UI.Code;

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
