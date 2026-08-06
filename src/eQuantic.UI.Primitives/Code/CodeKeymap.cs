namespace eQuantic.UI.Primitives;

/// <summary>
/// What a KEY means to an editor — the one place both targets agree.
/// <para>
/// The keyboard is where a code editor is most obviously right or wrong, and it is also where two
/// hand-written implementations drift fastest: the browser's keydown and the platform's key event
/// arrive with the same names, so the mapping from name to command belongs in neither of them. It
/// lives here, transpiles with everything else, and both surfaces call the same function.
/// </para>
/// <para>
/// Typed CHARACTERS do not come through here. What a keystroke produces is the platform's business
/// (a dead key, an input method, "á" from three events), so text arrives separately as a string and
/// goes to <see cref="CodeEditorController.Type"/>.
/// </para>
/// </summary>
public static class CodeKeymap
{
    /// <summary>
    /// Runs the command <paramref name="key"/> means, and answers whether the editor CLAIMED it.
    /// False means the key belongs to whatever is around the editor — Tab moving to the next
    /// control when nothing is selected, Escape closing the dialog the editor sits in.
    /// </summary>
    public static bool Handle(CodeEditorController editor, string key, KeyModifiers modifiers,
        ITextClipboard? clipboard = null)
    {
        var shift = (modifiers & KeyModifiers.Shift) != 0;
        var command = (modifiers & KeyModifiers.Command) != 0;
        var alt = (modifiers & KeyModifiers.Alt) != 0;

        // ---- the chords ------------------------------------------------------------------------
        if (command && key.Length == 1)
        {
            switch (char.ToLowerInvariant(key[0]))
            {
                case 'a': editor.SelectAll(); return true;
                case 'z': return shift ? editor.Redo() : editor.Undo();
                case 'y': return editor.Redo();
                // Claimed even with no clipboard behind them: letting ⌘V fall through would type a
                // "v" into the document, which is worse than nothing happening.
                case 'c': clipboard?.Write(editor.CopyText()); return true;
                case 'x': clipboard?.Write(editor.Cut()); return true;
                case 'v':
                    if (clipboard?.Read() is { Length: > 0 } pasted) editor.Insert(pasted);
                    return true;
                case '/': return editor.ToggleLineComment();
                default: return false;
            }
        }
        // ⌘/ arrives as the key "/" with no letter on some layouts, and as "Slash" on others.
        if (command && key is "/" or "Slash") return editor.ToggleLineComment();

        // ---- movement --------------------------------------------------------------------------
        // ⌘← / ⌘→ are line ends on Apple; ⌥← / ⌥→ are word steps. Both spellings of "the whole
        // document" (⌘↑/⌘↓) go to the ends, which is what every editor on the platform does.
        var motion = alt ? CodeMotion.Word : CodeMotion.Character;
        switch (key)
        {
            case "ArrowLeft":
                editor.Move(command ? CodeMotion.LineBoundary : motion, CodeDirection.Backward, shift);
                return true;
            case "ArrowRight":
                editor.Move(command ? CodeMotion.LineBoundary : motion, CodeDirection.Forward, shift);
                return true;
            case "ArrowUp":
                editor.Move(command ? CodeMotion.DocumentBoundary : CodeMotion.Line, CodeDirection.Backward, shift);
                return true;
            case "ArrowDown":
                editor.Move(command ? CodeMotion.DocumentBoundary : CodeMotion.Line, CodeDirection.Forward, shift);
                return true;
            case "Home":
                editor.Move(command ? CodeMotion.DocumentBoundary : CodeMotion.LineBoundary,
                    CodeDirection.Backward, shift);
                return true;
            case "End":
                editor.Move(command ? CodeMotion.DocumentBoundary : CodeMotion.LineBoundary,
                    CodeDirection.Forward, shift);
                return true;
            case "PageUp":
                editor.Move(CodeMotion.Page, CodeDirection.Backward, shift);
                return true;
            case "PageDown":
                editor.Move(CodeMotion.Page, CodeDirection.Forward, shift);
                return true;
        }

        // ---- editing ---------------------------------------------------------------------------
        switch (key)
        {
            case "Enter":
                return editor.InsertNewLine();
            case "Backspace":
                return editor.DeleteBackward(alt ? CodeMotion.Word : CodeMotion.Character);
            case "Delete":
                return editor.DeleteForward(alt ? CodeMotion.Word : CodeMotion.Character);
            case "Tab":
                // Tab INDENTS inside an editor, which is the whole point — but a caret with nothing
                // selected in a read-only view has nothing to indent, and there Tab still belongs
                // to the form.
                if (editor.ReadOnly) return false;
                return shift ? editor.Outdent() : editor.Indent();
            default:
                return false;
        }
    }
}
