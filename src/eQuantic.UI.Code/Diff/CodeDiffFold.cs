namespace eQuantic.UI.Code;

/// <summary>
/// A run of unchanged lines a diff layout folded: <see cref="Count"/> lines from
/// <see cref="OriginalLine"/> of the original side and from <see cref="ModifiedLine"/> of the modified
/// one. The original line is the run's name, which the layout's <c>expanded</c> set takes and an edit
/// of the modified side does not move.
/// </summary>
public readonly record struct CodeDiffFold(int OriginalLine, int ModifiedLine, int Count);
