using System.Text;

namespace eQuantic.UI.Primitives;

/// <summary>
/// The TEXT of a code editor, kept as LINES.
/// <para>
/// Lines rather than one string, because everything an editor does is line-shaped: the gutter
/// numbers them, the tokenizer colours them one at a time, the caret moves between them, and a
/// keystroke must not re-copy a megabyte. Every edit returns a NEW document — the old one is what
/// undo keeps, and a component that re-renders from a value never has to ask whether its copy is
/// stale.
/// </para>
/// <para>
/// Line endings are normalized to <c>\n</c> on the way in. What was pasted with CRLF stays valid
/// text; what the editor holds has one shape, because two shapes means every column arithmetic is
/// wrong on one of them.
/// </para>
/// </summary>
public sealed class CodeDocument
{
    private readonly List<string> _lines;

    private CodeDocument(List<string> lines) => _lines = lines;

    public static readonly CodeDocument Empty = new([string.Empty]);

    /// <summary>Parses text into lines, accepting CRLF, CR and LF alike.</summary>
    public static CodeDocument FromText(string text)
    {
        var lines = new List<string>();
        var current = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\r')
            {
                // CRLF is one break, not two — a lone CR is one too (old Mac files still exist).
                if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                lines.Add(current.ToString());
                current.Clear();
                continue;
            }
            if (c == '\n')
            {
                lines.Add(current.ToString());
                current.Clear();
                continue;
            }
            current.Append(c);
        }
        lines.Add(current.ToString());
        return new CodeDocument(lines);
    }

    public IReadOnlyList<string> Lines => _lines;
    public int LineCount => _lines.Count;

    /// <summary>The line at <paramref name="index"/>, or empty when the index is out of range —
    /// a caret that outlived an edit asks for lines that no longer exist, and answering with an
    /// exception would make every caller guard what this can simply answer.</summary>
    public string Line(int index) =>
        index >= 0 && index < _lines.Count ? _lines[index] : string.Empty;

    /// <summary>The whole document, lines joined by <c>\n</c>.</summary>
    public string Text => string.Join("\n", _lines);

    /// <summary>The number of characters in the document, counting one per line break.</summary>
    public int Length
    {
        get
        {
            var total = 0;
            for (var i = 0; i < _lines.Count; i++) total += _lines[i].Length + 1;
            return total - 1;
        }
    }

    /// <summary>The last place a caret can be.</summary>
    public CodePosition End => new(_lines.Count - 1, _lines[^1].Length);

    /// <summary>
    /// Pins a position INSIDE this document: a line past the end lands on the last line, a column
    /// past the end of its line lands at the end of it. Every navigation goes through here, which
    /// is why none of them has to think about the edges.
    /// </summary>
    public CodePosition Clamp(CodePosition position)
    {
        var line = Math.Clamp(position.Line, 0, _lines.Count - 1);
        var column = Math.Clamp(position.Column, 0, _lines[line].Length);
        return new CodePosition(line, column);
    }

    /// <summary>The character offset of a position — what a single-string API (the host's text
    /// pipeline, a clipboard, a find) speaks in.</summary>
    public int OffsetOf(CodePosition position)
    {
        var clamped = Clamp(position);
        var offset = 0;
        for (var i = 0; i < clamped.Line; i++) offset += _lines[i].Length + 1;
        return offset + clamped.Column;
    }

    /// <summary>The position of a character offset — the inverse of <see cref="OffsetOf"/>.</summary>
    public CodePosition PositionOf(int offset)
    {
        if (offset <= 0) return CodePosition.Start;
        var remaining = offset;
        for (var line = 0; line < _lines.Count; line++)
        {
            var length = _lines[line].Length;
            if (remaining <= length) return new CodePosition(line, remaining);
            remaining -= length + 1;             // the line, plus its break
        }
        return End;
    }

    /// <summary>The text covered by a range — what a copy puts on the clipboard.</summary>
    public string TextIn(CodeRange range)
    {
        var start = Clamp(range.Start);
        var end = Clamp(range.End);
        if (start == end) return string.Empty;
        if (start.Line == end.Line) return _lines[start.Line][start.Column..end.Column];

        var text = new StringBuilder();
        text.Append(_lines[start.Line][start.Column..]);
        for (var line = start.Line + 1; line < end.Line; line++)
        {
            text.Append('\n');
            text.Append(_lines[line]);
        }
        text.Append('\n');
        text.Append(_lines[end.Line][..end.Column]);
        return text.ToString();
    }

    /// <summary>
    /// Replaces <paramref name="range"/> with <paramref name="text"/> — the ONE edit primitive.
    /// Insert is an empty range, delete is empty text, and typing over a selection is both; a
    /// single primitive means undo has one shape to reverse and the tokenizer one thing to hear.
    /// </summary>
    public CodeDocument Replace(CodeRange range, string text, out CodePosition caret)
    {
        var start = Clamp(range.Start);
        var end = Clamp(range.End);

        var inserted = FromText(text);
        var lines = new List<string>(_lines.Count + inserted.LineCount);

        for (var i = 0; i < start.Line; i++) lines.Add(_lines[i]);

        var head = _lines[start.Line][..start.Column];
        var tail = _lines[end.Line][end.Column..];

        if (inserted.LineCount == 1)
        {
            lines.Add(head + inserted.Line(0) + tail);
            caret = new CodePosition(start.Line, start.Column + inserted.Line(0).Length);
        }
        else
        {
            lines.Add(head + inserted.Line(0));
            for (var i = 1; i < inserted.LineCount - 1; i++) lines.Add(inserted.Line(i));
            var last = inserted.Line(inserted.LineCount - 1);
            lines.Add(last + tail);
            caret = new CodePosition(start.Line + inserted.LineCount - 1, last.Length);
        }

        for (var i = end.Line + 1; i < _lines.Count; i++) lines.Add(_lines[i]);
        return new CodeDocument(lines);
    }

    /// <summary>The position one character BEFORE this one — which crosses a line break.</summary>
    public CodePosition Previous(CodePosition position)
    {
        var here = Clamp(position);
        if (here.Column > 0) return here with { Column = here.Column - 1 };
        if (here.Line == 0) return CodePosition.Start;
        return new CodePosition(here.Line - 1, _lines[here.Line - 1].Length);
    }

    /// <summary>The position one character AFTER this one.</summary>
    public CodePosition Next(CodePosition position)
    {
        var here = Clamp(position);
        if (here.Column < _lines[here.Line].Length) return here with { Column = here.Column + 1 };
        if (here.Line == _lines.Count - 1) return here;
        return new CodePosition(here.Line + 1, 0);
    }

    /// <summary>
    /// Where HOME goes: the first non-blank character, and only the true start when the caret is
    /// already there. Every editor does this, and once you have used it the plain version feels
    /// broken — indented code is where a caret wants to be, not column zero.
    /// </summary>
    public CodePosition LineStart(CodePosition position)
    {
        var here = Clamp(position);
        var line = _lines[here.Line];
        var indent = 0;
        while (indent < line.Length && (line[indent] == ' ' || line[indent] == '\t')) indent++;
        return here with { Column = here.Column == indent ? 0 : indent };
    }

    public CodePosition LineEnd(CodePosition position)
    {
        var here = Clamp(position);
        return here with { Column = _lines[here.Line].Length };
    }

    /// <summary>
    /// The WORD around a position — what a double click selects and what ⌥← / ⌥→ step over.
    /// A word is a run of letters, digits and underscores; anything else is its own "word", so a
    /// caret stepping through `foo.Bar(x)` stops where a reader would.
    /// </summary>
    public CodeRange WordAt(CodePosition position)
    {
        var here = Clamp(position);
        var line = _lines[here.Line];
        if (line.Length == 0) return new CodeRange(here);

        var index = Math.Min(here.Column, line.Length - 1);
        if (!IsWordChar(line[index]))
        {
            // On a symbol: select the run of symbols, so a caret in `=>` takes both.
            if (here.Column > 0 && IsWordChar(line[here.Column - 1])) index = here.Column - 1;
            else
            {
                var symbolStart = index;
                while (symbolStart > 0 && !IsWordChar(line[symbolStart - 1])
                       && !char.IsWhiteSpace(line[symbolStart - 1])) symbolStart--;
                var symbolEnd = index;
                while (symbolEnd < line.Length && !IsWordChar(line[symbolEnd])
                       && !char.IsWhiteSpace(line[symbolEnd])) symbolEnd++;
                return new CodeRange(here with { Column = symbolStart }, here with { Column = symbolEnd });
            }
        }

        var start = index;
        while (start > 0 && IsWordChar(line[start - 1])) start--;
        var end = index;
        while (end < line.Length && IsWordChar(line[end])) end++;
        return new CodeRange(here with { Column = start }, here with { Column = end });
    }

    /// <summary>The leading whitespace of a line — what a new line inherits (auto-indent).</summary>
    public string IndentOf(int line)
    {
        var text = Line(line);
        var indent = 0;
        while (indent < text.Length && (text[indent] == ' ' || text[indent] == '\t')) indent++;
        return text[..indent];
    }

    public static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
