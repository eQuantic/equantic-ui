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
/// not move. A folded row says what the view's <c>foldLabel</c> says for the count it hides: the
/// engine writes no text of the interface.
/// </para>
/// <para>
/// A patch's view has gaps, the lines of the file between two hunks that the patch left out: each is
/// one row on both sides, saying what the patch says there, the header of the hunk after it; and no
/// run is folded across one.
/// </para>
/// </summary>
public sealed record CodeDiffLayout(CodeRows Original, CodeRows Modified, IReadOnlyList<CodeDiffFold> Folds)
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
        int modifiedLines, int context = DefaultContext, IReadOnlyCollection<int>? expanded = null,
        IReadOnlyList<CodeDiffGap>? gaps = null, Func<int, string>? foldLabel = null)
    {
        var originalFillers = new List<CodeFiller>();
        var modifiedFillers = new List<CodeFiller>();
        AddGaps(gaps, originalFillers, modifiedFillers);
        foreach (var change in changes)
        {
            var difference = change.ModifiedCount - change.OriginalCount;
            if (difference > 0)
                originalFillers.Add(new CodeFiller(change.OriginalStart + change.OriginalCount, difference));
            else if (difference < 0)
                modifiedFillers.Add(new CodeFiller(change.ModifiedStart + change.ModifiedCount, -difference));
        }
        var (originalRuns, modifiedRuns, folds) = UnchangedRuns(changes, originalLines, modifiedLines, context, expanded,
            gaps, foldLabel);
        return new CodeDiffLayout(
            new CodeRows(originalLines, originalFillers, originalRuns),
            new CodeRows(modifiedLines, modifiedFillers, modifiedRuns), folds);
    }

    /// <summary>
    /// The rows of an inline view, on the modified text: before the lines a change added, the lines
    /// it removed, drawn from the original as fillers; and every long unchanged run folded.
    /// <see cref="Original"/> is the original's own rows, with the same runs folded, for a view that
    /// shows the original side too.
    /// </summary>
    public static CodeDiffLayout Inline(IReadOnlyList<CodeLineChange> changes, int originalLines,
        int modifiedLines, int context = DefaultContext, IReadOnlyCollection<int>? expanded = null,
        IReadOnlyList<CodeDiffGap>? gaps = null, Func<int, string>? foldLabel = null)
    {
        var originalGaps = new List<CodeFiller>();
        var removed = new List<CodeFiller>();
        AddGaps(gaps, originalGaps, removed);
        foreach (var change in changes)
        {
            if (change.OriginalCount > 0)
                removed.Add(new CodeFiller(change.ModifiedStart, change.OriginalCount, change.OriginalStart));
        }
        var (originalRuns, modifiedRuns, folds) = UnchangedRuns(changes, originalLines, modifiedLines, context, expanded,
            gaps, foldLabel);
        return new CodeDiffLayout(
            new CodeRows(originalLines, originalGaps, originalRuns),
            new CodeRows(modifiedLines, removed, modifiedRuns), folds);
    }

    /// <summary>One row on each side for each gap, saying what the patch says there.</summary>
    private static void AddGaps(IReadOnlyList<CodeDiffGap>? gaps, List<CodeFiller> original, List<CodeFiller> modified)
    {
        if (gaps is null) return;
        foreach (var gap in gaps)
        {
            original.Add(new CodeFiller(gap.OriginalLine, 1, Label: gap.Header));
            modified.Add(new CodeFiller(gap.ModifiedLine, 1, Label: gap.Header));
        }
    }

    /// <summary>
    /// The unchanged runs to fold on each side: between two changes (or a change and an end of the
    /// file), everything but <paramref name="context"/> lines next to each change, when that leaves at
    /// least <see cref="FewestFolded"/> lines to hide and the view has not opened the run.
    /// </summary>
    private static (List<CodeCollapse> Original, List<CodeCollapse> Modified, List<CodeDiffFold> Folds) UnchangedRuns(
        IReadOnlyList<CodeLineChange> changes, int originalLines, int modifiedLines, int context,
        IReadOnlyCollection<int>? expanded, IReadOnlyList<CodeDiffGap>? gaps, Func<int, string>? foldLabel)
    {
        var original = new List<CodeCollapse>();
        var modified = new List<CodeCollapse>();
        var folds = new List<CodeDiffFold>();
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
            if (hidden >= FewestFolded && !(expanded?.Contains(originalAt + before) ?? false)
                && !CrossesAGap(gaps, originalAt + before, originalAt + before + hidden))
            {
                var label = foldLabel?.Invoke(hidden);
                original.Add(new CodeCollapse(originalAt + before, originalAt + before + hidden - 1, Label: label));
                modified.Add(new CodeCollapse(modifiedAt + before, modifiedAt + before + hidden - 1, Label: label));
                folds.Add(new CodeDiffFold(originalAt + before, modifiedAt + before, hidden));
            }
            if (i < changes.Count)
            {
                originalAt = changes[i].OriginalStart + changes[i].OriginalCount;
                modifiedAt = changes[i].ModifiedStart + changes[i].ModifiedCount;
            }
        }
        return (original, modified, folds);
    }

    /// <summary>The fold that hides <paramref name="line"/> of the modified side, or null.</summary>
    public CodeDiffFold? FoldOfModified(int line)
    {
        foreach (var fold in Folds)
            if (line >= fold.ModifiedLine && line < fold.ModifiedLine + fold.Count) return fold;
        return null;
    }

    /// <summary>The fold that hides <paramref name="line"/> of the original side, or null.</summary>
    public CodeDiffFold? FoldOfOriginal(int line)
    {
        foreach (var fold in Folds)
            if (line >= fold.OriginalLine && line < fold.OriginalLine + fold.Count) return fold;
        return null;
    }

    /// <summary>Whether a gap stands inside lines <paramref name="from"/> to <paramref name="to"/> of
    /// the original side, or at their end: a fold across one would hide it.</summary>
    private static bool CrossesAGap(IReadOnlyList<CodeDiffGap>? gaps, int from, int to)
    {
        if (gaps is null) return false;
        foreach (var gap in gaps)
            if (gap.OriginalLine > from && gap.OriginalLine <= to) return true;
        return false;
    }
}
