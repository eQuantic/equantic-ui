namespace eQuantic.UI.Primitives;

/// <summary>
/// The colours of a document, kept up to date INCREMENTALLY.
/// <para>
/// A keystroke changes one line. Re-tokenizing the file for it is what makes an editor feel slow
/// on anything real, so this keeps the tokens per line plus the state each line ENDS in: when a
/// line changes, it re-tokenizes that line, and it continues down the file only while the ending
/// state keeps coming out different — which is exactly the case of a block comment being opened or
/// closed, and no other case at all.
/// </para>
/// </summary>
public sealed class CodeHighlighter
{
    private readonly List<List<CodeToken>> _tokens = [];
    private readonly List<int> _endStates = [];

    public CodeHighlighter(ICodeLanguage language) => Language = language;

    public ICodeLanguage Language { get; private set; }

    /// <summary>Swaps the language — everything re-colours, because everything means something else.</summary>
    public void UseLanguage(ICodeLanguage language)
    {
        Language = language;
        Invalidate();
    }

    /// <summary>Drops every cached line; the next read re-tokenizes what it needs.</summary>
    public void Invalidate()
    {
        _tokens.Clear();
        _endStates.Clear();
    }

    /// <summary>The tokens of one line, tokenizing (and caching) whatever is missing above it.</summary>
    public IReadOnlyList<CodeToken> TokensFor(CodeDocument document, int line)
    {
        if (line < 0 || line >= document.LineCount) return [];
        EnsureThrough(document, line);
        return _tokens[line];
    }

    /// <summary>
    /// Tells the highlighter a line CHANGED. Returns how far down the file the colours moved —
    /// usually just that line, and the rest of the file when a multi-line construct opened or
    /// closed. A caller repainting only what changed uses the number; a caller repainting
    /// everything can ignore it.
    /// </summary>
    public int LineChanged(CodeDocument document, int line, int linesInserted = 0, int linesRemoved = 0)
    {
        if (linesInserted != linesRemoved)
        {
            // The line COUNT moved: everything below shifted, and a cache indexed by line number
            // cannot shift with it cheaply. Drop from here down and let it refill lazily.
            TrimTo(line);
            return document.LineCount - 1;
        }

        if (line >= _tokens.Count) return line;

        var before = _endStates[line];
        Retokenize(document, line);
        if (_endStates[line] == before) return line;

        // The state at the end of this line changed, so the next line means something else now.
        for (var next = line + 1; next < _tokens.Count; next++)
        {
            var previous = _endStates[next];
            Retokenize(document, next);
            if (_endStates[next] == previous) return next;
        }
        return _tokens.Count - 1;
    }

    private void EnsureThrough(CodeDocument document, int line)
    {
        while (_tokens.Count <= line && _tokens.Count < document.LineCount)
        {
            var index = _tokens.Count;
            var state = index == 0 ? 0 : _endStates[index - 1];
            var tokens = new List<CodeToken>();
            var endState = Language.Tokenize(document.Line(index), state, tokens);
            _tokens.Add(tokens);
            _endStates.Add(endState);
        }
    }

    private void Retokenize(CodeDocument document, int line)
    {
        var state = line == 0 ? 0 : _endStates[line - 1];
        var tokens = _tokens[line];
        tokens.Clear();
        _endStates[line] = Language.Tokenize(document.Line(line), state, tokens);
    }

    private void TrimTo(int line)
    {
        if (line >= _tokens.Count) return;
        _tokens.RemoveRange(line, _tokens.Count - line);
        _endStates.RemoveRange(line, _endStates.Count - line);
    }
}
