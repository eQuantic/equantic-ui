namespace eQuantic.UI.Code;

/// <summary>
/// A region of a document that folds (#1119), LSP's <c>FoldingRange</c>: from the line that stays when it is
/// folded, its header, to the last line it hides. Folded, the lines after the header to <see cref="EndLine"/> are
/// hidden.
/// </summary>
public sealed record CodeFold(int StartLine, int EndLine)
{
    /// <summary>What a folded region shows after its header instead of <c>⋯</c>, LSP's <c>collapsedText</c>, or null
    /// for the marker.</summary>
    public string? Placeholder { get; init; }
}
