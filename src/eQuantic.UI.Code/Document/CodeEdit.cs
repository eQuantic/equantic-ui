namespace eQuantic.UI.Code;

/// <summary>
/// ONE replacement in a document, and everything needed to undo it: what was there, what replaced
/// it, and where the caret stood on each side. It is also what an editor's event carries, in the
/// <see cref="CodeChange"/> it raises — a host building an IDE listens to edits, not to keystrokes,
/// because a paste, a refactor and a typed character are the same thing to everything downstream (a
/// language server, a dirty flag, a diff).
/// <para>
/// <c>Typed</c> says a person TYPED it, a character at a time, which is what a run of undo joins.
/// A paste, a cut, an indent or a refactor arrives whole and is a step of its own.
/// </para>
/// </summary>
public sealed record CodeEdit(
    CodeRange Range,
    string RemovedText,
    string InsertedText,
    CodeRange SelectionBefore,
    CodeRange SelectionAfter,
    bool Typed)
{
    /// <summary>The range the inserted text occupies AFTER the edit — what a decoration or a
    /// language server has to shift to. Its lines break where the document breaks them, on CR, CRLF
    /// and LF alike, whoever made the edit: split on LF alone, a lone CR ended the range a line short
    /// of the text the document held, and undo took back the wrong text (#600).</summary>
    public CodeRange InsertedRange
    {
        get
        {
            var start = Range.Start;
            var lines = CodeDocument.FromText(InsertedText).Lines;
            var end = lines.Count == 1
                ? new CodePosition(start.Line, start.Column + lines[0].Length)
                : new CodePosition(start.Line + lines.Count - 1, lines[^1].Length);
            return new CodeRange(start, end);
        }
    }

    /// <summary>
    /// Where a range that stood at <paramref name="range"/> before this edit stands after it, as a mark
    /// travels with the text it marks: a problem's squiggle, a bookmark. A position before the edit
    /// stays; one after it shifts by what the edit removed and inserted; a start inside the replaced
    /// text goes to the start of the replacement and an end inside it to its end, so a range whose text
    /// was rewritten covers the rewrite, and one whose text was deleted is empty where the text was. A
    /// range never grows at its edges: text inserted where it starts or where it ends is not in it, and
    /// an empty range stays with the text after it.
    /// </summary>
    public CodeRange Carry(CodeRange range)
    {
        var start = CarryStart(range.Start);
        var end = CarryEnd(range.End);
        return new CodeRange(start, end < start ? start : end);
    }

    /// <summary>
    /// Where a place something is typed at, which stood at <paramref name="position"/> before this edit,
    /// stands after it: text inserted right at it goes after it, as the start of a word being completed
    /// keeps what is typed there, and inside the replaced text it goes to the end of the replacement.
    /// </summary>
    internal CodePosition CarryAnchor(CodePosition position) => CarryEnd(position);

    /// <summary>Where a range's start goes: past text inserted right at it, and to the start of a
    /// replacement it stood inside.</summary>
    private CodePosition CarryStart(CodePosition position)
    {
        if (position < Range.Start) return position;
        if (position == Range.Start && Range.IsEmpty) return Shifted(position);
        if (position < Range.End) return Range.Start;
        return Shifted(position);
    }

    /// <summary>Where a range's end goes: before text inserted right at it, and to the end of a
    /// replacement it stood inside.</summary>
    private CodePosition CarryEnd(CodePosition position)
    {
        if (position <= Range.Start) return position;
        if (position <= Range.End) return InsertedRange.End;
        return Shifted(position);
    }

    /// <summary>A position at or after the replaced text's end, moved by what the edit did there: on the
    /// line the replaced text ended on, after the inserted text's end; on a later line, by the lines the
    /// edit added or took away.</summary>
    private CodePosition Shifted(CodePosition position)
    {
        var inserted = InsertedRange.End;
        return position.Line == Range.End.Line
            ? new CodePosition(inserted.Line, inserted.Column + position.Column - Range.End.Column)
            : new CodePosition(position.Line + inserted.Line - Range.End.Line, position.Column);
    }

    /// <summary>True when this edit only ADDED text at the caret — the case undo coalesces, so a
    /// sentence typed letter by letter comes back in one press rather than forty.</summary>
    public bool IsSimpleInsert => RemovedText.Length == 0 && !BreaksLine;

    /// <summary>Whether the inserted text breaks a line, on CR, CRLF or LF, as the document breaks
    /// them: what ends a run of typing.</summary>
    internal bool BreaksLine => InsertedText.Contains('\n') || InsertedText.Contains('\r');
}
