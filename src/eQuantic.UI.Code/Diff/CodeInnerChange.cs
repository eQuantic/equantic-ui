namespace eQuantic.UI.Code;

/// <summary>
/// Words that changed inside a <see cref="CodeLineChange"/>: <see cref="Original"/> in the original
/// text became <see cref="Modified"/> in the modified one. Either range may be empty (an insertion or
/// a deletion at that position), and either may span lines, when a line was split or joined.
/// </summary>
public readonly record struct CodeInnerChange(CodeRange Original, CodeRange Modified);
