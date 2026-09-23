namespace eQuantic.UI.Code;

/// <summary>
/// ONE change to a document, and everything needed to undo it: what was there, what replaced it,
/// and where the caret stood on each side. It is also the EVENT an editor raises — a host building
/// an IDE listens to edits, not to keystrokes, because a paste, a refactor and a typed character
/// are the same thing to everything downstream (a language server, a dirty flag, a diff).
/// </summary>
public sealed record CodeEdit(
    CodeRange Range,
    string RemovedText,
    string InsertedText,
    CodeRange SelectionBefore,
    CodeRange SelectionAfter)
{
    /// <summary>The range the inserted text occupies AFTER the edit — what a decoration or a
    /// language server has to shift to.</summary>
    public CodeRange InsertedRange
    {
        get
        {
            var start = Range.Start;
            var lines = InsertedText.Split('\n');
            var end = lines.Length == 1
                ? new CodePosition(start.Line, start.Column + lines[0].Length)
                : new CodePosition(start.Line + lines.Length - 1, lines[^1].Length);
            return new CodeRange(start, end);
        }
    }

    /// <summary>True when this edit only ADDED text at the caret — the case undo coalesces, so a
    /// sentence typed letter by letter comes back in one press rather than forty.</summary>
    public bool IsSimpleInsert => RemovedText.Length == 0 && !InsertedText.Contains('\n');
}
