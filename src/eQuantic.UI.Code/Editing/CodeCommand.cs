namespace eQuantic.UI.Code;

/// <summary>
/// What an editor's key runs, by name: what a menu, a command palette or an app's own shortcut runs too,
/// through <see cref="CodeEditorController.Execute"/>. The keymap (<see cref="CodeKeymap"/>) is a table
/// from chords to these, so rebinding a key is replacing an entry, never writing a behaviour again.
/// <para>
/// Each command is whole on its own, as VS Code's are: a move that extends the selection is a command of
/// its own (<see cref="CursorLeftSelect"/>), not a flag on a binding. A command runs only when it has
/// something to do (the list's commands while a list shows), and answers whether it did, which is what
/// lets one chord try several in order.
/// </para>
/// <para>
/// Append-only: C# bakes an enum's value into the assembly that uses it, so a member is never moved or
/// removed (<c>EnumValueAbiTests</c>).
/// </para>
/// </summary>
public enum CodeCommand
{
    /// <summary>Nothing: bound to a chord, it gives that chord back to the host.</summary>
    None,

    // ---- the caret ------------------------------------------------------------------------------

    /// <summary>One character left.</summary>
    CursorLeft,
    /// <summary>One character left, extending the selection.</summary>
    CursorLeftSelect,
    /// <summary>One character right.</summary>
    CursorRight,
    /// <summary>One character right, extending the selection.</summary>
    CursorRightSelect,
    /// <summary>One line up.</summary>
    CursorUp,
    /// <summary>One line up, extending the selection.</summary>
    CursorUpSelect,
    /// <summary>One line down.</summary>
    CursorDown,
    /// <summary>One line down, extending the selection.</summary>
    CursorDownSelect,
    /// <summary>One word left.</summary>
    CursorWordLeft,
    /// <summary>One word left, extending the selection.</summary>
    CursorWordLeftSelect,
    /// <summary>One word right.</summary>
    CursorWordRight,
    /// <summary>One word right, extending the selection.</summary>
    CursorWordRightSelect,
    /// <summary>To the start of the line.</summary>
    CursorLineStart,
    /// <summary>To the start of the line, extending the selection.</summary>
    CursorLineStartSelect,
    /// <summary>To the end of the line.</summary>
    CursorLineEnd,
    /// <summary>To the end of the line, extending the selection.</summary>
    CursorLineEndSelect,
    /// <summary>To the start of the document.</summary>
    CursorDocumentStart,
    /// <summary>To the start of the document, extending the selection.</summary>
    CursorDocumentStartSelect,
    /// <summary>To the end of the document.</summary>
    CursorDocumentEnd,
    /// <summary>To the end of the document, extending the selection.</summary>
    CursorDocumentEndSelect,
    /// <summary>One page up.</summary>
    CursorPageUp,
    /// <summary>One page up, extending the selection.</summary>
    CursorPageUpSelect,
    /// <summary>One page down.</summary>
    CursorPageDown,
    /// <summary>One page down, extending the selection.</summary>
    CursorPageDownSelect,
    /// <summary>The whole document selected.</summary>
    SelectAll,

    // ---- editing --------------------------------------------------------------------------------

    /// <summary>A line break at the caret, the new line indented as the language says.</summary>
    NewLine,
    /// <summary>The selection, or the character left of the caret, deleted.</summary>
    DeleteLeft,
    /// <summary>The selection, or the character right of the caret, deleted.</summary>
    DeleteRight,
    /// <summary>The selection, or the word left of the caret, deleted.</summary>
    DeleteWordLeft,
    /// <summary>The selection, or the word right of the caret, deleted.</summary>
    DeleteWordRight,
    /// <summary>The selection, or everything left of the caret on its line, deleted.</summary>
    DeleteToLineStart,
    /// <summary>The lines of the selection indented, or an indent at the caret; not in a read-only view,
    /// and not once Escape has let the next Tab leave the editor.</summary>
    Indent,
    /// <summary>The lines of the selection outdented, on the same terms as <see cref="Indent"/>.</summary>
    Outdent,
    /// <summary>The lines of the selection commented, or uncommented when every one is.</summary>
    ToggleLineComment,
    /// <summary>The last edit undone.</summary>
    Undo,
    /// <summary>The last edit undone, done again.</summary>
    Redo,
    /// <summary>The selection, or the caret's line, written to the clipboard.</summary>
    Copy,
    /// <summary>The selection, or the caret's line, written to the clipboard and deleted.</summary>
    Cut,
    /// <summary>The clipboard's text written over the selection.</summary>
    Paste,

    // ---- the completion list ----------------------------------------------------------------------

    /// <summary>The list asked for at the caret.</summary>
    CompletionShow,
    /// <summary>The list's next entry, while a list shows more than one.</summary>
    CompletionNext,
    /// <summary>The list's previous entry, while a list shows more than one.</summary>
    CompletionPrevious,
    /// <summary>A page of the list down.</summary>
    CompletionNextPage,
    /// <summary>A page of the list up.</summary>
    CompletionPreviousPage,
    /// <summary>The entry selected in the list, written over the word.</summary>
    CompletionAccept,
    /// <summary>The entry selected in the list, written over the word only when that changes the text:
    /// what Enter does, so a word typed out in full ends its line.</summary>
    CompletionAcceptChangingText,
    /// <summary>The list closed.</summary>
    CompletionHide,

    // ---- the signature panel ----------------------------------------------------------------------

    /// <summary>The panel asked for at the caret.</summary>
    SignatureHelpShow,
    /// <summary>The panel's next overload, while it shows more than one and no card hides it.</summary>
    SignatureHelpNext,
    /// <summary>The panel's previous overload, on the same terms as <see cref="SignatureHelpNext"/>.</summary>
    SignatureHelpPrevious,
    /// <summary>The panel closed, and a completion list asked for and not yet showing dropped with it.</summary>
    SignatureHelpHide,

    // ---- the card and the problems ----------------------------------------------------------------

    /// <summary>The card for the caret, at once.</summary>
    HoverShow,
    /// <summary>The card closed, and a completion list asked for and not yet showing dropped with it.</summary>
    HoverHide,
    /// <summary>The caret on the next problem, its card shown.</summary>
    ProblemNext,
    /// <summary>The caret on the previous problem, its card shown.</summary>
    ProblemPrevious,

    // ---- the keyboard leaving -----------------------------------------------------------------------

    /// <summary>
    /// What Escape does when nothing over the code takes it: a list asked for and not yet showing is
    /// dropped, and the next Tab leaves the editor instead of indenting. It claims nothing, so the host
    /// goes on meaning what Escape means around the editor.
    /// </summary>
    ReleaseKeyboard,

    // ---- lines (#1011): appended, as every member after the first is ----------------------------------

    /// <summary>The lines the selection takes, one line up, past the line above them, the selection with them.</summary>
    MoveLinesUp,
    /// <summary>The lines the selection takes, one line down, past the line below them.</summary>
    MoveLinesDown,
    /// <summary>The lines copied above themselves, the selection on the upper copy.</summary>
    CopyLinesUp,
    /// <summary>The lines copied below themselves, the selection on the lower copy.</summary>
    CopyLinesDown,
    /// <summary>The lines deleted, a line break with them.</summary>
    DeleteLines,
    /// <summary>The lines joined into one, or the caret's line with the next, as VS Code joins them.</summary>
    JoinLines,
    /// <summary>The lines sorted, ordinally and case aside.</summary>
    SortLinesAscending,
    /// <summary>The lines sorted the other way.</summary>
    SortLinesDescending,
    /// <summary>A line opened above the caret's, indented as it is.</summary>
    InsertLineAbove,
    /// <summary>A line opened below the caret's, indented as Enter indents.</summary>
    InsertLineBelow,

    // ---- carets (#1012) ---------------------------------------------------------------------------------

    /// <summary>A caret added on the line above every caret, at the cell it aims at.</summary>
    AddCursorAbove,
    /// <summary>A caret added on the line below every caret.</summary>
    AddCursorBelow,
    /// <summary>The word at every caret selected, and then the next occurrence of the primary's text added as
    /// the primary, as ⌘D does.</summary>
    AddNextOccurrence,
    /// <summary>Every occurrence of the primary's text, or of the word at the caret, selected.</summary>
    SelectAllOccurrences,
    /// <summary>Every caret but the primary taken away. Claims nothing with one caret, so Escape goes on.</summary>
    RemoveSecondaryCursors,

    // ---- snippets (#1013) -------------------------------------------------------------------------------

    /// <summary>The snippet's next stop, every place it is written selected; the last one ends the snippet.</summary>
    NextTabStop,
    /// <summary>The snippet's stop before. Claims nothing at its first stop, so ⇧Tab outdents there.</summary>
    PreviousTabStop,
    /// <summary>The snippet ended, the carets where they are. Claims nothing with no snippet, so Escape goes on.</summary>
    LeaveSnippet,

    // ---- navigation (#1083) -----------------------------------------------------------------------------

    /// <summary>The go-to field opened on a line (<c>:</c>).</summary>
    GoToLine,
    /// <summary>The go-to field opened on the document's symbols (<c>@</c>).</summary>
    GoToSymbol,
    /// <summary>The place before in the jump list. Claims nothing at the first place.</summary>
    NavigateBack,
    /// <summary>The place after in the jump list. Claims nothing at the last place.</summary>
    NavigateForward,
}
