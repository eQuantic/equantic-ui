using eQuantic.UI.Primitives;

namespace eQuantic.UI.Code;

/// <summary>
/// One coloured stretch of ONE line: where it starts, how long it is, what it means. Line-local by
/// design — a tokenizer that answered in document offsets would have to re-run over the whole file
/// whenever a line changed, and an editor changes a line on every keystroke.
/// </summary>
public readonly record struct CodeToken(int Start, int Length, CodeTokenKind Kind)
{
    public int End => Start + Length;
}
