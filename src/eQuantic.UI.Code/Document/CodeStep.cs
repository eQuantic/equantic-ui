namespace eQuantic.UI.Code;

/// <summary>
/// One undo step: the edits one command made, in the order made, each in the coordinates of the document it
/// was made on, and every selection on each side of them, the primary first and the others in document
/// order. Undo takes the edits back and puts <see cref="Before"/> back; redo makes them again and puts
/// <see cref="After"/> back, so a command made at several carets is undone with all of them where they
/// stood (#1012).
/// </summary>
public sealed record CodeStep(IReadOnlyList<CodeEdit> Edits, IReadOnlyList<CodeRange> Before,
    IReadOnlyList<CodeRange> After);
