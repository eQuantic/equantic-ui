namespace eQuantic.UI.Code;

/// <summary>
/// What one change did to a document, as <see cref="CodeEditorController.Changed"/> raises it: the edits one
/// command, one undo or one redo made, in the order made, each range where the edits before it left the
/// text, as a language server's content changes are. The document is the change's result by the time it is
/// heard, so a listener carries its ranges through every edit and only then reads the text: heard an edit
/// at a time, a problem reported against the text a move left was carried through the move's second edit,
/// which it already stood after (#1058).
/// <para>
/// <see cref="Replaced"/> says the whole document was replaced, a file opened, and then there are no edits
/// to carry anything through: whatever was kept for the old text starts over.
/// </para>
/// </summary>
public sealed record CodeChange(IReadOnlyList<CodeEdit> Edits, bool Replaced = false);
