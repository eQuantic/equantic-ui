using eQuantic.UI.Primitives;

namespace eQuantic.UI.Code;

/// <summary>
/// An extra mark over a range that the LANGUAGE did not put there: the other occurrences of the
/// symbol under the caret, the current search match, the matching bracket, a diff hunk. Handed in
/// as data so the same editor serves an IDE, a diff viewer and a log reader.
/// </summary>
public sealed record CodeDecoration(CodeRange Range, CodeDecorationKind Kind = CodeDecorationKind.Highlight)
{
    /// <summary>Overrides the theme's colour for this kind — a diff green, a diagnostic red.</summary>
    public ColorToken? Color { get; init; }
}
