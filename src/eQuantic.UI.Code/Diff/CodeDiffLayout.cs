namespace eQuantic.UI.Code;

/// <summary>
/// The rows a diff is drawn on (docs/CODE-EDITOR-PLAN.md, the shape, §9): for the two sides of a
/// side-by-side view, <see cref="Original"/> and <see cref="Modified"/>, which take the same number
/// of rows and keep every change level; for an inline view, <see cref="Modified"/> alone, with the
/// lines each change removed drawn before the lines that replaced them.
/// <para>
/// Runs of unchanged lines longer than the context either side of a change can use are folded into
/// one row, on both sides at once, unless the view has opened them: both factories take the runs
/// opened, named by the first line each hides on the original side, which a change elsewhere does
/// not move.
/// </para>
/// </summary>
public sealed record CodeDiffLayout(CodeRows Original, CodeRows Modified)
{
    /// <summary>How many unchanged lines stay in view either side of a change.</summary>
    public const int DefaultContext = 3;

    /// <summary>A run is folded only when it hides at least this many lines: folding two lines
    /// into one row saves a row and costs a press.</summary>
    private const int FewestFolded = 3;

    /// <summary>
    /// The rows of both sides of a side-by-side view: at each change the side with fewer lines is
    /// padded to the other's, AFTER its own lines, so the two stay level; and every long unchanged
    /// run is folded on both.
    /// </summary>
    public static CodeDiffLayout SideBySide(IReadOnlyList<CodeLineChange> changes, int originalLines,
        int modifiedLines, int context = DefaultContext, IReadOnlyCollection<int>? expanded = null)
    {
        var originalFillers = new List<CodeFiller>();
        var modifiedFillers = new List<CodeFiller>();
        foreach (var change in changes)
        {
            var difference = change.ModifiedCount - change.OriginalCount;
            if (difference > 0)
                originalFillers.Add(new CodeFiller(change.OriginalStart + change.OriginalCount, difference));
            else if (difference < 0)
                modifiedFillers.Add(new CodeFiller(change.ModifiedStart + change.ModifiedCount, -difference));
        }
        var (originalRuns, modifiedRuns) = UnchangedRuns(changes, originalLines, modifiedLines, context, expanded);
        return new CodeDiffLayout(
            new CodeRows(originalLines, originalFillers, originalRuns),
            new CodeRows(modifiedLines, modifiedFillers, modifiedRuns));
    }

    /// <summary>
    /// The rows of an inline view, on the modified text: before the lines a change added, the lines
    /// it removed, drawn from the original as fillers; and every long unchanged run folded.
    /// <see cref="Original"/> is the original's own rows, with the same runs folded, for a view that
    /// shows the original side too.
    /// </summary>
    public static CodeDiffLayout Inline(IReadOnlyList<CodeLineChange> changes, int originalLines,
        int modifiedLines, int context = DefaultContext, IReadOnlyCollection<int>? expanded = null)
    {
        var removed = new List<CodeFiller>();
        foreach (var change in changes)
        {
            if (change.OriginalCount > 0)
                removed.Add(new CodeFiller(change.ModifiedStart, change.OriginalCount, change.OriginalStart));
        }
        var (originalRuns, modifiedRuns) = UnchangedRuns(changes, originalLines, modifiedLines, context, expanded);
        return new CodeDiffLayout(
            new CodeRows(originalLines, [], originalRuns),
            new CodeRows(modifiedLines, removed, modifiedRuns));
    }

    /// <summary>
    /// The unchanged runs to fold on each side: between two changes (or a change and an end of the
    /// file), everything but <paramref name="context"/> lines next to each change, when that leaves at
    /// least <see cref="FewestFolded"/> lines to hide and the view has not opened the run.
    /// </summary>
    private static (List<CodeCollapse> Original, List<CodeCollapse> Modified) UnchangedRuns(
        IReadOnlyList<CodeLineChange> changes, int originalLines, int modifiedLines, int context,
        IReadOnlyCollection<int>? expanded)
    {
        var original = new List<CodeCollapse>();
        var modified = new List<CodeCollapse>();
        var originalAt = 0;
        var modifiedAt = 0;
        for (var i = 0; i <= changes.Count; i++)
        {
            // The unchanged run from where the previous change ended to where this one starts (or
            // to the end of the file after the last change). The two sides share its length.
            var originalEnd = i < changes.Count ? changes[i].OriginalStart : originalLines;
            var modifiedEnd = i < changes.Count ? changes[i].ModifiedStart : modifiedLines;
            var length = Math.Min(originalEnd - originalAt, modifiedEnd - modifiedAt);
            // The file's own edges need no context: nothing is there to read it against.
            var before = i > 0 ? context : 0;
            var after = i < changes.Count ? context : 0;
            var hidden = length - before - after;
            if (hidden >= FewestFolded && !(expanded?.Contains(originalAt + before) ?? false))
            {
                original.Add(new CodeCollapse(originalAt + before, originalAt + before + hidden - 1));
                modified.Add(new CodeCollapse(modifiedAt + before, modifiedAt + before + hidden - 1));
            }
            if (i < changes.Count)
            {
                originalAt = changes[i].OriginalStart + changes[i].OriginalCount;
                modifiedAt = changes[i].ModifiedStart + changes[i].ModifiedCount;
            }
        }
        return (original, modified);
    }
}
