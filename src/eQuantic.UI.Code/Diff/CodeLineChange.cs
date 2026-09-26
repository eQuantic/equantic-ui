namespace eQuantic.UI.Code;

/// <summary>
/// One region where two texts differ, in lines: <see cref="OriginalCount"/> lines of the original
/// from <see cref="OriginalStart"/> became <see cref="ModifiedCount"/> lines of the modified text from
/// <see cref="ModifiedStart"/>. A count of 0 is an insertion (or a deletion) at that line. The
/// <see cref="Inner"/> changes say which words within the region changed, in each side's own
/// positions, so a diff can mark the words and not only the lines.
/// </summary>
public sealed record CodeLineChange(
    int OriginalStart,
    int OriginalCount,
    int ModifiedStart,
    int ModifiedCount,
    IReadOnlyList<CodeInnerChange> Inner);
