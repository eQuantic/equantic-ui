using eQuantic.UI.Primitives;

namespace eQuantic.UI.Code;

/// <summary>
/// What a KEY means to an editor — the one place both targets agree.
/// <para>
/// The keyboard is where a code editor is most obviously right or wrong, and it is also where two
/// hand-written implementations drift fastest: the browser's keydown and the platform's key event
/// arrive with the same names, so the mapping from name to command belongs in neither of them. It
/// lives here, transpiles with everything else, and both surfaces call the same function.
/// </para>
/// <para>
/// It speaks both keyboard traditions (<see cref="KeyboardConvention"/>), because they disagree on
/// exactly the keys an editor lives on: ⌘← is "line start" on a Mac and Ctrl+← is "one word back"
/// everywhere else, and <see cref="KeyModifiers.Command"/> is ⌘ on one and Ctrl on the other. A
/// keymap that knew only one had Windows and Linux users jumping to the start of the line every time
/// they meant to step over a word.
/// </para>
/// <para>
/// Typed CHARACTERS do not come through here. What a keystroke produces is the platform's business
/// (a dead key, an input method, "á" from three events), so text arrives separately as a string and
/// goes to <see cref="CodeEditorController.HandleText"/>.
/// </para>
/// </summary>
public static class CodeKeymap
{
    /// <summary>
    /// Runs the command <paramref name="key"/> means, and answers whether the editor CLAIMED it.
    /// False means the key belongs to whatever is around the editor — Tab moving to the next
    /// control once Escape has released it, Escape closing the dialog the editor sits in.
    /// </summary>
    public static bool Handle(CodeEditorController editor, string key, KeyModifiers modifiers,
        KeyboardConvention convention, ITextClipboard? clipboard = null)
    {
        var shift = (modifiers & KeyModifiers.Shift) != 0;
        var command = (modifiers & KeyModifiers.Command) != 0;
        var alt = (modifiers & KeyModifiers.Alt) != 0;
        var apple = convention == KeyboardConvention.Apple;

        // ---- the Tab trap --------------------------------------------------------------------
        // An editor TAKES Tab, which is the point — and a keyboard user must still be able to leave
        // it. Escape is the door (the handoff's rule for every editing surface): it releases the
        // trap, so the NEXT Tab moves focus on instead of indenting. Unclaimed, so the host can go on
        // meaning what Escape means around the editor. Any other key re-arms the trap; a modifier on
        // its own is not "another key", or Shift+Tab could never leave backwards.
        if (key == "Escape")
        {
            editor.TabMovesFocus = true;
            return false;
        }
        if (key != "Tab" && !IsModifierKey(key)) editor.TabMovesFocus = false;

        // ---- the chords ------------------------------------------------------------------------
        if (command && key.Length == 1)
        {
            switch (char.ToLowerInvariant(key[0]))
            {
                case 'a': editor.SelectAll(); return true;
                case 'z': return shift ? editor.Redo() : editor.Undo();
                case 'y': return editor.Redo();
                // With no clipboard HERE, the host's clipboard arrives as events of its own (a
                // browser's copy, cut and paste), and those carry the text: claiming the key would
                // cancel the very event that brings it. With one, the key is claimed even when it
                // finds nothing to paste — falling through could only do something the user did not
                // ask for.
                case 'c':
                    if (clipboard is null) return false;
                    clipboard.Write(editor.CopyText());
                    return true;
                case 'x':
                    if (clipboard is null) return false;
                    clipboard.Write(editor.Cut());
                    return true;
                case 'v':
                    if (clipboard is null) return false;
                    if (clipboard.Read() is { Length: > 0 } pasted) editor.Paste(pasted);
                    return true;
                case '/': return editor.ToggleLineComment();
                default: return false;
            }
        }
        // ⌘/ arrives as the key "/" with no letter on some layouts, and as "Slash" on others.
        if (command && key is "/" or "Slash") return editor.ToggleLineComment();

        // ---- movement --------------------------------------------------------------------------
        // WORD steps are ⌥ on Apple and Ctrl (Command, there) everywhere else. LINE and DOCUMENT
        // ends are ⌘+arrows on Apple; everywhere else they are Home/End, with Ctrl for the document.
        var byWord = apple ? alt : command;
        switch (key)
        {
            case "ArrowLeft":
                editor.Move(apple && command ? CodeMotion.LineBoundary
                    : byWord ? CodeMotion.Word : CodeMotion.Character, CodeDirection.Backward, shift);
                return true;
            case "ArrowRight":
                editor.Move(apple && command ? CodeMotion.LineBoundary
                    : byWord ? CodeMotion.Word : CodeMotion.Character, CodeDirection.Forward, shift);
                return true;
            case "ArrowUp":
                // Ctrl+↑ scrolls the view in every Standard editor; there is no view to scroll from
                // here, so it is left to the host rather than guessed at.
                if (!apple && command) return false;
                editor.Move(apple && command ? CodeMotion.DocumentBoundary : CodeMotion.Line,
                    CodeDirection.Backward, shift);
                return true;
            case "ArrowDown":
                if (!apple && command) return false;
                editor.Move(apple && command ? CodeMotion.DocumentBoundary : CodeMotion.Line,
                    CodeDirection.Forward, shift);
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
                // ⌘⌫ on Apple takes everything left of the caret on its line.
                return editor.DeleteBackward(apple && command ? CodeMotion.LineBoundary
                    : byWord ? CodeMotion.Word : CodeMotion.Character);
            case "Delete":
                return editor.DeleteForward(byWord ? CodeMotion.Word : CodeMotion.Character);
            case "Tab":
                // Tab INDENTS inside an editor, which is the whole point — unless Escape released
                // it, or a read-only view has nothing to indent: there Tab belongs to the form.
                if (editor.ReadOnly || editor.TabMovesFocus) return false;
                return shift ? editor.Outdent() : editor.Indent();
            default:
                return false;
        }
    }

    /// <summary>A key that only modifies others — pressing it is not a key of its own.</summary>
    private static bool IsModifierKey(string key) =>
        key is "Shift" or "Control" or "Alt" or "Meta" or "AltGraph" or "CapsLock" or "Fn" or "OS";
}
