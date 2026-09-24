namespace eQuantic.UI.Code;

/// <summary>
/// What a diff view shows: an <see cref="Original"/> and a <see cref="Modified"/> text, what differs
/// between them (<see cref="Changes"/>), and the number each line has in its file.
/// <para>
/// From two texts (<see cref="FromTexts"/>) they are whole files, a line's number is its index plus
/// one, and the changes are <see cref="CodeDiffer"/>'s. From a patch (<see cref="FromPatch"/>) they
/// are the lines the patch quotes, hunk after hunk, numbered as the patch numbers them; the changes
/// are the ones the patch marks, a run of removed and added lines between two lines of context
/// being one change, with its words compared; and between two hunks a <see cref="CodeDiffGap"/> says
/// how many lines of the file the patch left out.
/// </para>
/// </summary>
public sealed class CodeDiffSource
{
    private readonly IReadOnlyList<int>? _originalNumbers;
    private readonly IReadOnlyList<int>? _modifiedNumbers;

    private CodeDiffSource(CodeDocument original, int originalLineCount, CodeDocument modified, int modifiedLineCount,
        IReadOnlyList<CodeLineChange> changes, IReadOnlyList<int>? originalNumbers, IReadOnlyList<int>? modifiedNumbers,
        IReadOnlyList<CodeDiffGap> gaps)
    {
        Original = original;
        OriginalLineCount = originalLineCount;
        Modified = modified;
        ModifiedLineCount = modifiedLineCount;
        Changes = changes;
        _originalNumbers = originalNumbers;
        _modifiedNumbers = modifiedNumbers;
        Gaps = gaps;
    }

    /// <summary>The original text, or the lines of it a patch quotes.</summary>
    public CodeDocument Original { get; }

    /// <summary>The modified text, or the lines of it a patch quotes.</summary>
    public CodeDocument Modified { get; }

    /// <summary>
    /// How many lines each side has for the view, which is the document's own count for a whole text
    /// and 0 for the side of a patch that quotes nothing, a file added or deleted, where the document
    /// still holds its one empty line. The rows of a view are counted on these.
    /// </summary>
    public int OriginalLineCount { get; }

    /// <inheritdoc cref="OriginalLineCount"/>
    public int ModifiedLineCount { get; }

    /// <summary>What differs, in the lines of <see cref="Original"/> and <see cref="Modified"/>.</summary>
    public IReadOnlyList<CodeLineChange> Changes { get; }

    /// <summary>The runs of the file a patch left out: none for two whole texts.</summary>
    public IReadOnlyList<CodeDiffGap> Gaps { get; }

    /// <summary>The number line <paramref name="line"/> of <see cref="Original"/> has in its file,
    /// counted from 1 as a gutter counts.</summary>
    public int OriginalNumber(int line) => _originalNumbers is { } numbers ? numbers[line] : line + 1;

    /// <summary>The number line <paramref name="line"/> of <see cref="Modified"/> has in its file.</summary>
    public int ModifiedNumber(int line) => _modifiedNumbers is { } numbers ? numbers[line] : line + 1;

    /// <summary>Two whole texts, compared.</summary>
    public static CodeDiffSource FromTexts(string original, string modified)
    {
        var before = CodeDocument.FromText(original);
        var after = CodeDocument.FromText(modified);
        return new CodeDiffSource(before, before.LineCount, after, after.LineCount, CodeDiffer.Compare(before, after),
            null, null, []);
    }

    /// <summary>The lines one file of a patch quotes, and the changes it marks.</summary>
    public static CodeDiffSource FromPatch(CodePatchFile file)
    {
        var originalLines = new List<string>();
        var modifiedLines = new List<string>();
        var originalNumbers = new List<int>();
        var modifiedNumbers = new List<int>();
        var changes = new List<CodeLineChange>();
        var gaps = new List<CodeDiffGap>();
        var originalEnd = 0;
        var modifiedEnd = 0;
        foreach (var hunk in file.Hunks)
        {
            var skippedOriginal = hunk.OriginalStart - originalEnd;
            var skippedModified = hunk.ModifiedStart - modifiedEnd;
            if (skippedOriginal > 0 || skippedModified > 0)
                gaps.Add(new CodeDiffGap(originalLines.Count, modifiedLines.Count, skippedOriginal, skippedModified));

            var originalNumber = hunk.OriginalStart;
            var modifiedNumber = hunk.ModifiedStart;
            var i = 0;
            while (i < hunk.Lines.Count)
            {
                if (hunk.Lines[i].Kind == CodePatchLineKind.Context)
                {
                    originalLines.Add(hunk.Lines[i].Text);
                    originalNumbers.Add(++originalNumber);
                    modifiedLines.Add(hunk.Lines[i].Text);
                    modifiedNumbers.Add(++modifiedNumber);
                    i++;
                    continue;
                }
                // A run of removed and added lines, up to the next line of context, is one change.
                var originalStart = originalLines.Count;
                var modifiedStart = modifiedLines.Count;
                while (i < hunk.Lines.Count && hunk.Lines[i].Kind != CodePatchLineKind.Context)
                {
                    if (hunk.Lines[i].Kind == CodePatchLineKind.Removed)
                    {
                        originalLines.Add(hunk.Lines[i].Text);
                        originalNumbers.Add(++originalNumber);
                    }
                    else
                    {
                        modifiedLines.Add(hunk.Lines[i].Text);
                        modifiedNumbers.Add(++modifiedNumber);
                    }
                    i++;
                }
                var originalCount = originalLines.Count - originalStart;
                var modifiedCount = modifiedLines.Count - modifiedStart;
                changes.Add(new CodeLineChange(originalStart, originalCount, modifiedStart, modifiedCount,
                    CodeDiffer.InnerChanges(originalLines, originalStart, originalCount, modifiedLines, modifiedStart, modifiedCount)));
            }
            originalEnd = hunk.OriginalStart + hunk.OriginalCount;
            modifiedEnd = hunk.ModifiedStart + hunk.ModifiedCount;
        }
        return new CodeDiffSource(CodeDocument.FromLines(originalLines), originalLines.Count,
            CodeDocument.FromLines(modifiedLines), modifiedLines.Count, changes, originalNumbers, modifiedNumbers, gaps);
    }
}
