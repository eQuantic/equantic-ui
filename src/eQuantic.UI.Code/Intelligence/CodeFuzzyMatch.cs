namespace eQuantic.UI.Code;

/// <summary>
/// How well a typed PATTERN matches a WORD, and which of the word's characters it matched: the
/// filter of a completion list, and of anything else that narrows a list as a person types (a
/// command palette, a file picker). Written once, so a page and an IDE on Photon rank the same list
/// the same way.
/// <para>
/// The pattern's characters must appear in the word in order, case aside, and the first of them
/// where a part of the word starts: its first character, a character after a separator, or the
/// capital that begins a part of a camel-cased name. So <c>col</c> finds <c>Column</c>, <c>bc</c>
/// finds <c>BarChart</c>, <c>xh</c> finds <c>XmlHttp</c>, and <c>lum</c> finds nothing.
/// </para>
/// <para>
/// Of every way the pattern can be laid over the word, the best one counts. Each matched character
/// scores 1, one more when it is in the case it was typed in, 8 more on the word's first character,
/// 6 more where a part of the word starts, and 5 more right after the character matched before it;
/// each gap between two matched characters costs 3. So a run beats the same characters apart
/// (<c>col</c> ranks <c>Column</c> over <c>CopyLine</c>), and since the search goes through every
/// pattern character against every word character, <c>cou</c> over <c>ConsoleOutput</c> takes the
/// <c>Ou</c> that starts a part, not the first <c>o</c>.
/// </para>
/// </summary>
public sealed record CodeFuzzyMatch(int Score, IReadOnlyList<int> Positions)
{
    /// <summary>
    /// The best way <paramref name="pattern"/> lies over <paramref name="word"/>, or null when it does
    /// not. An empty pattern matches every word, scoring nothing. With
    /// <paramref name="anywhere"/>, the first character may land anywhere: what a list uses to mark
    /// a label that was not the text it matched.
    /// </summary>
    public static CodeFuzzyMatch? Of(string pattern, string word, bool anywhere = false)
    {
        var m = pattern.Length;
        var n = word.Length;
        if (m == 0) return new CodeFuzzyMatch(0, []);
        if (m > n || !InOrder(pattern, word)) return null;

        // score[i * n + j]: the best score of pattern[0..i] with pattern[i] on word[j], or -1 where it
        // cannot be. from[i * n + j]: where pattern[i - 1] lies on that best way.
        var score = new int[m * n];
        var from = new int[m * n];
        for (var i = 0; i < m; i++)
        {
            var typed = pattern[i];
            var lower = char.ToLowerInvariant(typed);
            // The best of the row above at least two characters back, and where: the match before
            // this one with a gap between them.
            var best = -1;
            var bestAt = -1;
            for (var j = 0; j < n; j++)
            {
                var at = i * n + j;
                score[at] = -1;
                from[at] = -1;
                if (i > 0 && j >= 2 && score[(i - 1) * n + j - 2] > best)
                {
                    best = score[(i - 1) * n + j - 2];
                    bestAt = j - 2;
                }
                if (j < i || char.ToLowerInvariant(word[j]) != lower) continue;

                var start = j == 0 || StartsPart(word, j);
                var gain = 1 + (word[j] == typed ? 1 : 0) + (j == 0 ? 8 : start ? 6 : 0);
                if (i == 0)
                {
                    if (start || anywhere) score[at] = gain;
                    continue;
                }
                var adjacent = j > 0 ? score[(i - 1) * n + j - 1] : -1;
                if (adjacent >= 0)
                {
                    score[at] = adjacent + gain + 5;
                    from[at] = j - 1;
                }
                if (best >= 0 && best + gain - 3 > score[at])
                {
                    score[at] = best + gain - 3;
                    from[at] = bestAt;
                }
            }
        }

        var last = (m - 1) * n;
        var end = -1;
        for (var j = m - 1; j < n; j++)
        {
            if (score[last + j] >= 0 && (end < 0 || score[last + j] > score[last + end])) end = j;
        }
        if (end < 0) return null;

        var positions = new int[m];
        var on = end;
        for (var i = m - 1; i >= 0; i--)
        {
            positions[i] = on;
            on = from[i * n + on];
        }
        return new CodeFuzzyMatch(score[last + end], positions);
    }

    /// <summary>Whether the pattern's characters appear in the word in order, case aside: the cheap
    /// test that turns most of a long list away before the search costs anything.</summary>
    private static bool InOrder(string pattern, string word)
    {
        var i = 0;
        for (var j = 0; j < word.Length && i < pattern.Length; j++)
        {
            if (char.ToLowerInvariant(word[j]) == char.ToLowerInvariant(pattern[i])) i++;
        }
        return i == pattern.Length;
    }

    /// <summary>
    /// Whether a part of the word starts at <paramref name="j"/> (never 0, which is the word's own
    /// start): a letter or digit after a separator, a capital after a lower-case letter or a digit,
    /// or the last capital of a run that a lower-case letter follows (the <c>H</c> of
    /// <c>XMLHttp</c>).
    /// </summary>
    private static bool StartsPart(string word, int j)
    {
        var before = word[j - 1];
        var at = word[j];
        if (!char.IsLetterOrDigit(before)) return char.IsLetterOrDigit(at);
        if (!char.IsUpper(at)) return false;
        return !char.IsUpper(before) || (j + 1 < word.Length && char.IsLower(word[j + 1]));
    }
}
