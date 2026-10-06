namespace eQuantic.UI.Code;

/// <summary>
/// The words already in the document, each once: what a plain-text editor completes, and what any
/// editor offers before (or without) a language service. The word the caret is in is the one being
/// typed, and is left out; so is a number.
/// <para>
/// It is asked at every word started, and answers before the keystroke returns, so it reads a
/// BOUNDED part of the document: the lines around the caret, the nearest first, until
/// <see cref="Budget"/> characters have been read. A file of a thousand lines or so is read whole;
/// in a longer one, what is left out is what lies farthest from the word being typed. It read every
/// line, and a file of 45,000 lines cost 46 ms in Bun at every word started.
/// </para>
/// </summary>
public sealed class CodeWordCompletionProvider : ICodeCompletionProvider
{
    /// <summary>How many characters one answer reads at most, the end of a line counting as one.</summary>
    internal const int Budget = 50_000;

    public Task<CodeCompletionList> CompleteAsync(CodeDocument document, CodePosition position,
        CodeCompletionContext context, CancellationToken cancellation)
    {
        var seen = new HashSet<string>();
        var items = new List<CodeCompletionItem>();
        var left = Budget;
        // The caret's line, then the line above it and the line below, and so outward.
        for (var distance = 0; left > 0; distance++)
        {
            var above = position.Line - distance;
            var below = position.Line + distance;
            if (above < 0 && below >= document.LineCount) break;
            if (above >= 0)
                left = Read(document.Line(above), above == position.Line ? position.Column : -1, left, seen, items);
            if (distance > 0 && below < document.LineCount && left > 0)
                left = Read(document.Line(below), -1, left, seen, items);
        }
        return Task.FromResult(new CodeCompletionList(items));
    }

    /// <summary>
    /// Adds the words of <paramref name="text"/> the answer does not have yet, reading no more than
    /// <paramref name="left"/> of its characters, and answers how many are left after it. The word
    /// <paramref name="caret"/> is in (a column, or -1) is left out, and so is a word the budget cuts:
    /// a part of a word is not one.
    /// <para>
    /// The caret's own line is read around the caret, the budget's half either side where the line
    /// allows it. Read from its start, a line longer than the budget (a minified file is one) gave the
    /// words farthest from the caret and none of those beside it.
    /// </para>
    /// </summary>
    private static int Read(string text, int caret, int left, HashSet<string> seen, List<CodeCompletionItem> items)
    {
        var from = caret < 0 ? 0 : Math.Max(0, Math.Min(caret - left / 2, text.Length - left));
        // A window that starts inside a word starts after it.
        if (from > 0 && CodeDocument.IsWordChar(text[from - 1]))
        {
            while (from < text.Length && CodeDocument.IsWordChar(text[from])) from++;
        }
        var end = Math.Min(text.Length, from + left);
        var i = from;
        while (i < end)
        {
            if (!CodeDocument.IsWordChar(text[i]))
            {
                i++;
                continue;
            }
            var start = i;
            while (i < end && CodeDocument.IsWordChar(text[i])) i++;
            if (start <= caret && caret <= i) continue;
            if (i == end && end < text.Length && CodeDocument.IsWordChar(text[end])) continue;
            if (char.IsDigit(text[start])) continue;
            var word = text.Substring(start, i - start);
            if (seen.Add(word)) items.Add(new CodeCompletionItem(word));
        }
        return left - (end - from) - 1;
    }
}
