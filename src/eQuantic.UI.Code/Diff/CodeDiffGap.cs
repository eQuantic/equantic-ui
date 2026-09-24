namespace eQuantic.UI.Code;

/// <summary>
/// Lines of a file that a patch left out, standing before line <see cref="OriginalLine"/> of the
/// original side and <see cref="ModifiedLine"/> of the modified side of a <see cref="CodeDiffSource"/>:
/// <see cref="OriginalCount"/> lines of the original file, and <see cref="ModifiedCount"/> of the
/// modified one, which differ when a hunk before them added or removed lines.
/// </summary>
public readonly record struct CodeDiffGap(int OriginalLine, int ModifiedLine, int OriginalCount, int ModifiedCount);
