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
/// The mapping is DATA (#1010): a table of <see cref="CodeKeyBinding"/>s from chords to
/// <see cref="CodeCommand"/>s, one for each keyboard tradition (<see cref="KeyboardConvention"/>), because
/// they disagree on exactly the keys an editor lives on: ⌘← is "line start" on a Mac and Ctrl+← is "one
/// word back" everywhere else, and <see cref="KeyModifiers.Command"/> is ⌘ on one and Ctrl on the other.
/// An app puts its own bindings over the table (<see cref="CodeEditorController.KeyBindings"/>), and
/// runs a command with no key at all (<see cref="CodeEditorController.Execute"/>).
/// </para>
/// <para>
/// A chord names its modifiers exactly. Written by hand, the keymap read them loosely and claimed ⌃⌥⇧←
/// on a Mac as a step left and ⌘⌥A as select all; a chord the table does not name is the host's.
/// </para>
/// <para>
/// Typed CHARACTERS do not come through here. What a keystroke produces is the platform's business
/// (a dead key, an input method, "á" from three events), so text arrives separately as a string and
/// goes to <see cref="CodeEditorController.HandleText"/>.
/// </para>
/// </summary>
public static class CodeKeymap
{
    /// <summary>The default bindings for a Mac's keyboard: ⌥ steps over words, ⌘ reaches line and
    /// document ends.</summary>
    private static readonly List<CodeKeyBinding> AppleBindings = Build(apple: true);

    /// <summary>The default bindings everywhere else: Ctrl (Command, here) steps over words, Home and End
    /// reach line ends, and Ctrl with them the document's.</summary>
    private static readonly List<CodeKeyBinding> StandardBindings = Build(apple: false);

    /// <summary>
    /// The bindings an editor answers with no app binding over them, for <paramref name="convention"/>, in
    /// the order a chord's commands are tried: what an IDE lists in its own sheet of shortcuts. A copy, so
    /// sorting it for a sheet changes no editor's keys.
    /// </summary>
    public static IReadOnlyList<CodeKeyBinding> Defaults(KeyboardConvention convention) => [.. Table(convention)];

    private static List<CodeKeyBinding> Table(KeyboardConvention convention) =>
        convention == KeyboardConvention.Apple ? AppleBindings : StandardBindings;

    /// <summary>
    /// Runs the command <paramref name="key"/> means, and answers whether the editor CLAIMED it.
    /// False means the key belongs to whatever is around the editor — Tab moving to the next
    /// control once Escape has released it, Escape closing the dialog the editor sits in.
    /// </summary>
    public static bool Handle(CodeEditorController editor, string key, KeyModifiers modifiers,
        KeyboardConvention convention, ITextClipboard? clipboard = null)
    {
        // ---- the Tab trap --------------------------------------------------------------------
        // An editor TAKES Tab, which is the point — and a keyboard user must still be able to leave
        // it. Escape is the door (the handoff's rule for every editing surface): it releases the
        // trap (ReleaseKeyboard), so the NEXT Tab moves focus on instead of indenting. Any other key
        // re-arms it, and does so before anything else here can claim the key: the list's own keys and
        // ⌃Space return early, and re-arming after them left Escape, ⌃Space, Escape (the list asked
        // for, then closed) with Tab leaving the editor. A modifier on its own is not "another key",
        // or Shift+Tab could never leave backwards.
        if (key != "Tab" && key != "Escape" && !IsModifierKey(key)) editor.TabMovesFocus = false;
        if (IsModifierKey(key)) return false;

        // ---- a chord half typed ------------------------------------------------------------------
        // Any key but a modifier ends the wait for a chord's second half (⌘K ⌘I), and ends it here, for
        // the reason the trap above re-arms here: ending the wait after the commands that claim a key
        // left ⌘K, ⌃Space, Escape, ⌘I showing the card.
        var first = editor.PendingChord;
        editor.PendingChord = null;

        var chord = new KeyChord(key, modifiers);
        var bindings = Bindings(editor, convention);
        // The second half of a chord of two keys. It is the chord's even when none of its commands did
        // anything, as VS Code has it: the key that completes a chord is never handed on as itself. A key
        // that is not the second half is handled as itself.
        if (first is { } waited)
        {
            var completes = false;
            foreach (var binding in bindings)
            {
                if (binding.First is not { } starts || !Matches(starts, waited) || !Matches(binding.Chord, chord))
                    continue;
                completes = true;
                if (editor.Execute(binding.Command, clipboard)) return true;
            }
            if (completes) return true;
        }

        // ---- the chord on its own, and the first half of a chord of two keys --------------------------
        // In the table's order, so an app's binding of either kind comes before every default: an app's
        // ⌘A ⌘D starts on ⌘A though the defaults select all there. A first half waits only while its
        // second half's command could run: with no card to show, a browser's Ctrl+K stays the browser's.
        foreach (var binding in bindings)
        {
            if (binding.First is { } starts)
            {
                if (!Matches(starts, chord) || !editor.CanExecute(binding.Command)) continue;
                editor.PendingChord = chord;
                return true;
            }
            if (Matches(binding.Chord, chord) && editor.Execute(binding.Command, clipboard)) return true;
        }
        return false;
    }

    /// <summary>
    /// The bindings a key is looked up in: the app's, then the defaults, without a default the app's
    /// bindings already name the chord of, so a chord the app binds is the app's alone. A key the app binds
    /// on its own takes the defaults' chords of two keys that start on it as well: ⌘K bound to
    /// <see cref="CodeCommand.None"/> is the host's, and the default ⌘K ⌘I started there all the same
    /// (Copilot's second round on #1027). A chord of the app's own that starts where a default's does
    /// leaves the default alone: both wait on the same first half.
    /// </summary>
    private static IReadOnlyList<CodeKeyBinding> Bindings(CodeEditorController editor, KeyboardConvention convention)
    {
        var defaults = Table(convention);
        var own = editor.KeyBindings;
        if (own.Count == 0) return defaults;
        var bindings = new List<CodeKeyBinding>(own);
        foreach (var binding in defaults)
        {
            var named = false;
            foreach (var mine in own)
            {
                var sameChord = Matches(mine.Chord, binding.Chord) && SameFirst(mine.First, binding.First);
                var startsOnMine = mine.First is null && binding.First is { } starts && Matches(mine.Chord, starts);
                if (!sameChord && !startsOnMine) continue;
                named = true;
                break;
            }
            if (!named) bindings.Add(binding);
        }
        return bindings;
    }

    /// <summary>Whether a key pressed is the chord bound: its modifiers exactly, a single letter in either
    /// case (⇧ with ⌘ reports "Z").</summary>
    private static bool Matches(KeyChord bound, KeyChord pressed)
    {
        if (bound.Modifiers != pressed.Modifiers) return false;
        if (bound.Key == pressed.Key) return true;
        return bound.Key.Length == 1 && pressed.Key.Length == 1
            && char.ToLowerInvariant(bound.Key[0]) == char.ToLowerInvariant(pressed.Key[0]);
    }

    private static bool SameFirst(KeyChord? bound, KeyChord? pressed)
    {
        if (bound is not { } one) return pressed is null;
        return pressed is { } other && Matches(one, other);
    }

    /// <summary>A key that only modifies others — pressing it is not a key of its own.</summary>
    private static bool IsModifierKey(string key) =>
        key is "Shift" or "Control" or "Alt" or "Meta" or "AltGraph" or "CapsLock" or "Fn" or "OS";

    // ---- the tables ---------------------------------------------------------------------------------

    private static CodeKeyBinding Bind(string key, KeyModifiers modifiers, CodeCommand command) =>
        new(new KeyChord(key, modifiers), command);

    /// <summary>
    /// The defaults for one convention, in the order a chord's commands are tried: what is over the code
    /// (the completion list, then the signature panel, then a card) before what moves the caret.
    /// </summary>
    private static List<CodeKeyBinding> Build(bool apple)
    {
        const KeyModifiers none = KeyModifiers.None;
        const KeyModifiers shift = KeyModifiers.Shift;
        const KeyModifiers command = KeyModifiers.Command;
        const KeyModifiers control = KeyModifiers.Control;
        const KeyModifiers alt = KeyModifiers.Alt;
        // WORD steps are ⌥ on Apple and Ctrl (Command, there) everywhere else. LINE and DOCUMENT ends are
        // ⌘+arrows on Apple; everywhere else they are Home/End, with Ctrl for the document.
        var word = apple ? alt : command;
        var bindings = new List<CodeKeyBinding>
        {
            // ---- an open completion list: its keys while it shows, as VS Code has them. An arrow goes
            // back to the caret when one entry shows. Enter accepts only what would change the text, so a
            // word typed out in full ends its line.
            Bind("ArrowDown", none, CodeCommand.CompletionNext),
            Bind("ArrowUp", none, CodeCommand.CompletionPrevious),
            Bind("PageDown", none, CodeCommand.CompletionNextPage),
            Bind("PageUp", none, CodeCommand.CompletionPreviousPage),
            Bind("Tab", none, CodeCommand.CompletionAccept),
            Bind("Enter", none, CodeCommand.CompletionAcceptChangingText),
            // ⌃Space, which a browser reports as the command key and a Mac's own shell as Control.
            Bind(" ", command, CodeCommand.CompletionShow),
            Bind(" ", control, CodeCommand.CompletionShow),

            // ---- the signature panel: ⇧⌘Space asks at the caret; ↑ and ↓ walk the overloads while it
            // shows more than one and no card hides it, and ⌥↑ ⌥↓ do it beside a list, which keeps the
            // plain arrows.
            Bind(" ", shift | command, CodeCommand.SignatureHelpShow),
            Bind(" ", shift | control, CodeCommand.SignatureHelpShow),
            Bind("ArrowDown", none, CodeCommand.SignatureHelpNext),
            Bind("ArrowUp", none, CodeCommand.SignatureHelpPrevious),
            Bind("ArrowDown", alt, CodeCommand.SignatureHelpNext),
            Bind("ArrowUp", alt, CodeCommand.SignatureHelpPrevious),

            // ---- the card and the problems: ⌘K ⌘I shows the card at the caret, and F8 and ⇧F8 walk the
            // problems.
            new(new KeyChord("i", command), CodeCommand.HoverShow) { First = new KeyChord("k", command) },
            Bind("F8", none, CodeCommand.ProblemNext),
            Bind("F8", shift, CodeCommand.ProblemPrevious),

            // ---- Escape closes what is over the code, the list, then a card, then the panel, then leaves a
            // snippet, then takes away every caret but the primary, one at a time, so the trap on Tab stays
            // armed and a dialog around the editor stays open; with nothing over the code and one caret it
            // releases the trap and claims nothing. A list asked for and not yet showing goes with whichever of
            // them Escape closes (HoverHide, SignatureHelpHide, LeaveSnippet, ReleaseKeyboard).
            Bind("Escape", none, CodeCommand.CompletionHide),
            Bind("Escape", none, CodeCommand.HoverHide),
            Bind("Escape", none, CodeCommand.SignatureHelpHide),
            Bind("Escape", none, CodeCommand.LeaveSnippet),
            Bind("Escape", none, CodeCommand.RemoveSecondaryCursors),
            Bind("Escape", none, CodeCommand.ReleaseKeyboard),
            Bind("Escape", shift, CodeCommand.CompletionHide),
            Bind("Escape", shift, CodeCommand.HoverHide),
            Bind("Escape", shift, CodeCommand.SignatureHelpHide),
            Bind("Escape", shift, CodeCommand.LeaveSnippet),
            Bind("Escape", shift, CodeCommand.RemoveSecondaryCursors),
            Bind("Escape", shift, CodeCommand.ReleaseKeyboard),

            // ---- the letter chords. ⌘/ arrives as "/" or as "Slash", and with ⇧ on a layout that types
            // the slash with it.
            Bind("a", command, CodeCommand.SelectAll),
            Bind("z", command, CodeCommand.Undo),
            Bind("z", shift | command, CodeCommand.Redo),
            Bind("y", command, CodeCommand.Redo),
            Bind("c", command, CodeCommand.Copy),
            Bind("x", command, CodeCommand.Cut),
            Bind("v", command, CodeCommand.Paste),
            Bind("/", command, CodeCommand.ToggleLineComment),
            Bind("/", shift | command, CodeCommand.ToggleLineComment),
            Bind("Slash", command, CodeCommand.ToggleLineComment),
            Bind("Slash", shift | command, CodeCommand.ToggleLineComment),

            // ---- the caret, a character and a line at a time, and a word.
            Bind("ArrowLeft", none, CodeCommand.CursorLeft),
            Bind("ArrowLeft", shift, CodeCommand.CursorLeftSelect),
            Bind("ArrowRight", none, CodeCommand.CursorRight),
            Bind("ArrowRight", shift, CodeCommand.CursorRightSelect),
            Bind("ArrowUp", none, CodeCommand.CursorUp),
            Bind("ArrowUp", shift, CodeCommand.CursorUpSelect),
            Bind("ArrowDown", none, CodeCommand.CursorDown),
            Bind("ArrowDown", shift, CodeCommand.CursorDownSelect),
            Bind("ArrowLeft", word, CodeCommand.CursorWordLeft),
            Bind("ArrowLeft", shift | word, CodeCommand.CursorWordLeftSelect),
            Bind("ArrowRight", word, CodeCommand.CursorWordRight),
            Bind("ArrowRight", shift | word, CodeCommand.CursorWordRightSelect),

            // ---- line and document ends: Home and End everywhere, ⌘ with them for the document.
            Bind("Home", none, CodeCommand.CursorLineStart),
            Bind("Home", shift, CodeCommand.CursorLineStartSelect),
            Bind("End", none, CodeCommand.CursorLineEnd),
            Bind("End", shift, CodeCommand.CursorLineEndSelect),
            Bind("Home", command, CodeCommand.CursorDocumentStart),
            Bind("Home", shift | command, CodeCommand.CursorDocumentStartSelect),
            Bind("End", command, CodeCommand.CursorDocumentEnd),
            Bind("End", shift | command, CodeCommand.CursorDocumentEndSelect),
            Bind("PageUp", none, CodeCommand.CursorPageUp),
            Bind("PageUp", shift, CodeCommand.CursorPageUpSelect),
            Bind("PageDown", none, CodeCommand.CursorPageDown),
            Bind("PageDown", shift, CodeCommand.CursorPageDownSelect),

            // ---- editing.
            Bind("Enter", none, CodeCommand.NewLine),
            Bind("Enter", shift, CodeCommand.NewLine),
            Bind("Backspace", none, CodeCommand.DeleteLeft),
            Bind("Backspace", shift, CodeCommand.DeleteLeft),
            Bind("Backspace", word, CodeCommand.DeleteWordLeft),
            Bind("Delete", none, CodeCommand.DeleteRight),
            Bind("Delete", word, CodeCommand.DeleteWordRight),
            // Tab walks a snippet's stops while one is being filled in, after the list's own Tab and before
            // indenting; ⇧Tab at its first stop goes on to outdent, as VS Code's has no stop to go back to.
            Bind("Tab", none, CodeCommand.NextTabStop),
            Bind("Tab", shift, CodeCommand.PreviousTabStop),
            // Tab INDENTS inside an editor, which is the whole point — unless Escape released it, or a
            // read-only view has nothing to indent: there Tab belongs to the form.
            Bind("Tab", none, CodeCommand.Indent),
            Bind("Tab", shift, CodeCommand.Outdent),

            // ---- lines, as VS Code has them: ⌥↑ ⌥↓ move them, named after the panel's own ⌥↑ ⌥↓ so it
            // keeps them while it shows overloads; ⇧⌥↑ ⇧⌥↓ copy them; ⇧⌘K deletes them; ⌘↩ and ⇧⌘↩ open a
            // line below and above the caret's.
            Bind("ArrowUp", alt, CodeCommand.MoveLinesUp),
            Bind("ArrowDown", alt, CodeCommand.MoveLinesDown),
            Bind("ArrowUp", shift | alt, CodeCommand.CopyLinesUp),
            Bind("ArrowDown", shift | alt, CodeCommand.CopyLinesDown),
            Bind("k", shift | command, CodeCommand.DeleteLines),
            Bind("Enter", command, CodeCommand.InsertLineBelow),
            Bind("Enter", shift | command, CodeCommand.InsertLineAbove),

            // ---- carets, as VS Code has them (#1012): ⌥⌘↑ ⌥⌘↓ add one above or below every caret (Ctrl+Alt
            // with the arrows on Windows), ⌘D adds the next occurrence and ⇧⌘L selects every one.
            Bind("ArrowUp", alt | command, CodeCommand.AddCursorAbove),
            Bind("ArrowDown", alt | command, CodeCommand.AddCursorBelow),
            Bind("d", command, CodeCommand.AddNextOccurrence),
            Bind("l", shift | command, CodeCommand.SelectAllOccurrences),
        };
        if (apple)
        {
            // ⌘← and ⌘→ reach the line's ends, ⌘↑ and ⌘↓ the document's, and ⌘⌫ takes everything left of
            // the caret on its line.
            bindings.Add(Bind("ArrowLeft", command, CodeCommand.CursorLineStart));
            bindings.Add(Bind("ArrowLeft", shift | command, CodeCommand.CursorLineStartSelect));
            bindings.Add(Bind("ArrowRight", command, CodeCommand.CursorLineEnd));
            bindings.Add(Bind("ArrowRight", shift | command, CodeCommand.CursorLineEndSelect));
            bindings.Add(Bind("ArrowUp", command, CodeCommand.CursorDocumentStart));
            bindings.Add(Bind("ArrowUp", shift | command, CodeCommand.CursorDocumentStartSelect));
            bindings.Add(Bind("ArrowDown", command, CodeCommand.CursorDocumentEnd));
            bindings.Add(Bind("ArrowDown", shift | command, CodeCommand.CursorDocumentEndSelect));
            bindings.Add(Bind("Backspace", command, CodeCommand.DeleteToLineStart));
            // ⌃J joins the lines, a Mac's alone as in VS Code, and on ⌘ too, as ⌃Space is: the browser
            // reports Ctrl as the command key. On Windows and Linux, where VS Code binds no join, Ctrl+J
            // stays the browser's.
            bindings.Add(Bind("j", control, CodeCommand.JoinLines));
            bindings.Add(Bind("j", command, CodeCommand.JoinLines));
        }
        // Ctrl+↑ scrolls the view in every Standard editor; there is no view to scroll from here, so it
        // is left to the host rather than guessed at.
        return bindings;
    }
}
