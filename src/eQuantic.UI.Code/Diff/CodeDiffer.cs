namespace eQuantic.UI.Code;

/// <summary>
/// What differs between two texts: the lines, and within each changed region the words. What a diff
/// view draws, and what an IDE reads to show a file against its last commit.
/// <para>
/// The line diff is Myers' shortest edit script, the one git computes with <c>--minimal</c>: as few
/// lines removed and added as the two texts allow. It is found by the middle snake, walking from both
/// ends at once in linear space, over a range whose common head and tail are trimmed first, so the
/// cost follows the size of the change rather than the size of the file. Lines are compared as
/// whole strings, through one number per distinct line.
/// </para>
/// <para>
/// The word diff runs the same algorithm over the tokens of a changed region, its lines joined by a
/// line break of their own, so a line split in two or two lines joined read as the break they are.
/// A token is a run of word characters, a run of whitespace, or any other character by itself.
/// </para>
/// <para>
/// <em>How does Flutter solve it?</em> It has no diff. VS Code computes line changes with inner
/// changes in each one's own positions, and so does this.
/// </para>
/// </summary>
public static class CodeDiffer
{
    /// <summary>A region with more tokens than this on a side is a rewrite, and marking its words would
    /// mark all of them, so it carries no inner changes.</summary>
    private const int InnerTokenLimit = 20_000;

    /// <summary>
    /// How many rounds a middle snake is searched for. A shortest script costs time that grows with the
    /// square of how different two ranges are, and ranges this far apart are a rewrite: past it, every
    /// line of both is marked, which is a longer script than the shortest and still a right one. No
    /// diff view is worth freezing for, and a count of rounds, unlike a clock, answers the same on
    /// every machine and on both sides.
    /// </summary>
    private const int MaxRounds = 2_000;

    /// <summary>What differs between two documents.</summary>
    public static IReadOnlyList<CodeLineChange> Compare(CodeDocument original, CodeDocument modified) =>
        CompareLines(original.Lines, modified.Lines);

    /// <summary>What differs between two texts given as their lines.</summary>
    public static IReadOnlyList<CodeLineChange> CompareLines(IReadOnlyList<string> original, IReadOnlyList<string> modified)
    {
        var ids = new Dictionary<string, int>();
        var a = IdsOf(original, ids);
        var b = IdsOf(modified, ids);
        var removed = new bool[a.Length];
        var added = new bool[b.Length];
        Diff(a, 0, a.Length, b, 0, b.Length, removed, added);

        var changes = new List<CodeLineChange>();
        var i = 0;
        var j = 0;
        while (i < a.Length || j < b.Length)
        {
            if (i < a.Length && j < b.Length && !removed[i] && !added[j])
            {
                i++;
                j++;
                continue;
            }
            var fromI = i;
            var fromJ = j;
            while (i < a.Length && removed[i]) i++;
            while (j < b.Length && added[j]) j++;
            if (i == fromI && j == fromJ) throw new InvalidOperationException("The two sides of the diff lost their alignment.");
            changes.Add(new CodeLineChange(fromI, i - fromI, fromJ, j - fromJ,
                InnerChanges(original, fromI, i - fromI, modified, fromJ, j - fromJ)));
        }
        return changes;
    }

    /// <summary>One number per distinct string, shared by both sides, so the diff compares numbers.
    /// The next number is counted here rather than read from the dictionary's count, which the web
    /// counts by walking every key.</summary>
    private static int[] IdsOf(IReadOnlyList<string> items, Dictionary<string, int> ids)
    {
        var result = new int[items.Count];
        var next = ids.Count;
        for (var i = 0; i < items.Count; i++)
        {
            if (!ids.TryGetValue(items[i], out var id))
            {
                id = next++;
                ids[items[i]] = id;
            }
            result[i] = id;
        }
        return result;
    }

    /// <summary>
    /// Marks what the shortest edit script removes from <paramref name="a"/> between
    /// <paramref name="aLo"/> and <paramref name="aHi"/>, and adds from <paramref name="b"/> between
    /// <paramref name="bLo"/> and <paramref name="bHi"/>.
    /// </summary>
    private static void Diff(int[] a, int aLo, int aHi, int[] b, int bLo, int bHi, bool[] removed, bool[] added)
    {
        // The common head and tail are no part of any change, and trimming them keeps the ranges the
        // middle snake walks as small as the change.
        while (aLo < aHi && bLo < bHi && a[aLo] == b[bLo])
        {
            aLo++;
            bLo++;
        }
        while (aLo < aHi && bLo < bHi && a[aHi - 1] == b[bHi - 1])
        {
            aHi--;
            bHi--;
        }
        if (aLo == aHi)
        {
            for (var j = bLo; j < bHi; j++) added[j] = true;
            return;
        }
        if (bLo == bHi)
        {
            for (var i = aLo; i < aHi; i++) removed[i] = true;
            return;
        }
        Bisect(a, aLo, aHi, b, bLo, bHi, removed, added);
    }

    /// <summary>
    /// Finds the middle snake of the two ranges, walking a path forward from their start and one
    /// backward from their end, one step of each per round, until the two overlap: the point where they
    /// meet lies on a shortest edit script, and the two halves on either side of it are diffed apart.
    /// Myers, "An O(ND) Difference Algorithm and Its Variations" (1986), section 4b, as the
    /// diff-match-patch library writes it. When the two ranges share nothing, or the search runs past
    /// <see cref="MaxRounds"/>, the whole of one is removed and the whole of the other added.
    /// </summary>
    private static void Bisect(int[] a, int aLo, int aHi, int[] b, int bLo, int bHi, bool[] removed, bool[] added)
    {
        var n = aHi - aLo;
        var m = bHi - bLo;
        var maxD = (n + m + 1) / 2;
        // The walks go no further than MaxRounds diagonals either way, so neither do the arrays: sized
        // from maxD alone, two long unrelated texts allocated two arrays as long as both of them,
        // for rounds that were never going to run. What lies past the reach was never written, and
        // every read that could land there is bounded below.
        var reach = Math.Min(maxD, MaxRounds);
        var offset = reach;
        // Two slots of headroom: the walks read one diagonal past the last one they write.
        var length = 2 * reach + 2;
        var forward = new int[length];
        var reverse = new int[length];
        for (var k = 0; k < length; k++)
        {
            forward[k] = -1;
            reverse[k] = -1;
        }
        forward[offset + 1] = 0;
        reverse[offset + 1] = 0;
        var delta = n - m;
        // With an odd difference the forward walk sees the overlap first, with an even one the reverse.
        var front = delta % 2 != 0;
        var k1Start = 0;
        var k1End = 0;
        var k2Start = 0;
        var k2End = 0;
        for (var d = 0; d < maxD && d < MaxRounds; d++)
        {
            for (var k1 = -d + k1Start; k1 <= d - k1End; k1 += 2)
            {
                var k1Offset = offset + k1;
                var x1 = k1 == -d || (k1 != d && forward[k1Offset - 1] < forward[k1Offset + 1])
                    ? forward[k1Offset + 1]
                    : forward[k1Offset - 1] + 1;
                var y1 = x1 - k1;
                while (x1 < n && y1 < m && a[aLo + x1] == b[bLo + y1])
                {
                    x1++;
                    y1++;
                }
                forward[k1Offset] = x1;
                if (x1 > n)
                {
                    k1End += 2;
                }
                else if (y1 > m)
                {
                    k1Start += 2;
                }
                else if (front)
                {
                    var k2Offset = offset + delta - k1;
                    if (k2Offset >= 0 && k2Offset < length && reverse[k2Offset] != -1 && x1 >= n - reverse[k2Offset])
                    {
                        Diff(a, aLo, aLo + x1, b, bLo, bLo + y1, removed, added);
                        Diff(a, aLo + x1, aHi, b, bLo + y1, bHi, removed, added);
                        return;
                    }
                }
            }

            for (var k2 = -d + k2Start; k2 <= d - k2End; k2 += 2)
            {
                var k2Offset = offset + k2;
                var x2 = k2 == -d || (k2 != d && reverse[k2Offset - 1] < reverse[k2Offset + 1])
                    ? reverse[k2Offset + 1]
                    : reverse[k2Offset - 1] + 1;
                var y2 = x2 - k2;
                while (x2 < n && y2 < m && a[aHi - x2 - 1] == b[bHi - y2 - 1])
                {
                    x2++;
                    y2++;
                }
                reverse[k2Offset] = x2;
                if (x2 > n)
                {
                    k2End += 2;
                }
                else if (y2 > m)
                {
                    k2Start += 2;
                }
                else if (!front)
                {
                    var k1Offset = offset + delta - k2;
                    if (k1Offset >= 0 && k1Offset < length && forward[k1Offset] != -1)
                    {
                        var x1 = forward[k1Offset];
                        var y1 = x1 - (k1Offset - offset);
                        if (x1 >= n - x2)
                        {
                            Diff(a, aLo, aLo + x1, b, bLo, bLo + y1, removed, added);
                            Diff(a, aLo + x1, aHi, b, bLo + y1, bHi, removed, added);
                            return;
                        }
                    }
                }
            }
        }

        // No overlap: the two ranges share nothing, or are too far apart to search (MaxRounds).
        for (var i = aLo; i < aHi; i++) removed[i] = true;
        for (var j = bLo; j < bHi; j++) added[j] = true;
    }

    /// <summary>
    /// The words that changed within a region, in each side's positions. None for a region that is
    /// only removed or only added (the whole region is the change) or that is a rewrite too large to
    /// mark word by word.
    /// </summary>
    private static IReadOnlyList<CodeInnerChange> InnerChanges(IReadOnlyList<string> original, int originalStart,
        int originalCount, IReadOnlyList<string> modified, int modifiedStart, int modifiedCount)
    {
        if (originalCount == 0 || modifiedCount == 0) return [];

        var aTexts = new List<string>();
        var aLines = new List<int>();
        var aColumns = new List<int>();
        Tokenize(original, originalStart, originalCount, aTexts, aLines, aColumns);
        var bTexts = new List<string>();
        var bLines = new List<int>();
        var bColumns = new List<int>();
        Tokenize(modified, modifiedStart, modifiedCount, bTexts, bLines, bColumns);
        if (aTexts.Count > InnerTokenLimit || bTexts.Count > InnerTokenLimit) return [];

        var ids = new Dictionary<string, int>();
        var a = IdsOf(aTexts, ids);
        var b = IdsOf(bTexts, ids);
        var removed = new bool[a.Length];
        var added = new bool[b.Length];
        Diff(a, 0, a.Length, b, 0, b.Length, removed, added);

        var originalEnd = new CodePosition(originalStart + originalCount - 1, original[originalStart + originalCount - 1].Length);
        var modifiedEnd = new CodePosition(modifiedStart + modifiedCount - 1, modified[modifiedStart + modifiedCount - 1].Length);
        var inner = new List<CodeInnerChange>();
        var i = 0;
        var j = 0;
        while (i < a.Length || j < b.Length)
        {
            if (i < a.Length && j < b.Length && !removed[i] && !added[j])
            {
                i++;
                j++;
                continue;
            }
            var fromI = i;
            var fromJ = j;
            while (i < a.Length && removed[i]) i++;
            while (j < b.Length && added[j]) j++;
            if (i == fromI && j == fromJ) throw new InvalidOperationException("The two sides of the diff lost their alignment.");
            inner.Add(new CodeInnerChange(
                Span(aTexts, aLines, aColumns, fromI, i, originalEnd),
                Span(bTexts, bLines, bColumns, fromJ, j, modifiedEnd)));
        }
        return inner;
    }

    /// <summary>
    /// The tokens of <paramref name="count"/> lines from <paramref name="start"/>: runs of word
    /// characters, runs of whitespace, and every other character (a surrogate pair whole) by itself,
    /// with a line break between two lines. Each token's text, line and column go to the three lists.
    /// </summary>
    private static void Tokenize(IReadOnlyList<string> lines, int start, int count,
        List<string> texts, List<int> tokenLines, List<int> tokenColumns)
    {
        for (var line = start; line < start + count; line++)
        {
            if (line > start)
            {
                texts.Add("\n");
                tokenLines.Add(line - 1);
                tokenColumns.Add(lines[line - 1].Length);
            }
            var text = lines[line];
            var column = 0;
            while (column < text.Length)
            {
                var begin = column;
                if (CodeDocument.IsWordChar(text[column]))
                {
                    while (column < text.Length && CodeDocument.IsWordChar(text[column])) column++;
                }
                else if (char.IsWhiteSpace(text[column]))
                {
                    while (column < text.Length && char.IsWhiteSpace(text[column])) column++;
                }
                else
                {
                    column += column + 1 < text.Length && char.IsSurrogatePair(text[column], text[column + 1]) ? 2 : 1;
                }
                texts.Add(text.Substring(begin, column - begin));
                tokenLines.Add(line);
                tokenColumns.Add(begin);
            }
        }
    }

    /// <summary>
    /// Where tokens <paramref name="from"/> up to <paramref name="to"/> lie, as a range: from the
    /// first one's start to the last one's end (a line break ends at the start of the next line). An
    /// empty run stands where the next token begins, or at <paramref name="end"/> past the last.
    /// </summary>
    private static CodeRange Span(List<string> texts, List<int> lines, List<int> columns, int from, int to, CodePosition end)
    {
        var start = from < texts.Count ? new CodePosition(lines[from], columns[from]) : end;
        if (to == from) return new CodeRange(start);
        var last = to - 1;
        var finish = texts[last] == "\n"
            ? new CodePosition(lines[last] + 1, 0)
            : new CodePosition(lines[last], columns[last] + texts[last].Length);
        return new CodeRange(start, finish);
    }
}
