namespace eQuantic.UI.Primitives;

/// <summary>How far one caret movement goes.</summary>
public enum CodeMotion : byte
{
    Character = 0, Word = 1, Line = 2, LineBoundary = 3, DocumentBoundary = 4, Page = 5,
}

/// <summary>Which way it goes.</summary>
public enum CodeDirection : byte { Backward = 0, Forward = 1 }

/// <summary>
/// THE EDITOR, minus the pixels.
/// <para>
/// Everything an editing surface does — moving, selecting, typing, indenting, commenting, undoing
/// — is a method here, on a document and a selection, with no idea what any of it looks like. That
/// is what makes it testable without a screen, identical on both targets, and usable by an IDE
/// that wants to drive the editor from its own menu, its own key map or its own language server.
/// </para>
/// <para>
/// The app owns an instance. Every mutation raises <see cref="Changed"/> with the <see cref="CodeEdit"/>
/// that caused it, which is what a dirty flag, a language server and a diff all subscribe to —
/// never to keystrokes, because a paste and a refactor are edits nobody typed.
/// </para>
/// </summary>
public sealed class CodeEditorController
{
    private CodeDocument _document;
    private CodeRange _selection;

    public CodeEditorController(string text = "", ICodeLanguage? language = null)
    {
        _document = CodeDocument.FromText(text);
        _selection = new CodeRange(CodePosition.Start);
        Highlighter = new CodeHighlighter(language ?? CodeLanguages.PlainText);
    }

    /// <summary>The text, as lines. Replacing it is a full reset — a file was opened.</summary>
    public CodeDocument Document
    {
        get => _document;
        set
        {
            _document = value;
            _selection = new CodeRange(_document.Clamp(_selection.Focus));
            Highlighter.Invalidate();
            History.Clear();
            Changed?.Invoke(null);
        }
    }

    /// <summary>Where the caret is, and what it has selected.</summary>
    public CodeRange Selection
    {
        get => _selection;
        set
        {
            var next = new CodeRange(_document.Clamp(value.Anchor), _document.Clamp(value.Focus));
            if (next == _selection) return;
            // Moving the caret ENDS the typing run: the next character starts a new undo step,
            // because a person who moved and typed did two things.
            History.Break();
            _selection = next;
            SelectionChanged?.Invoke(next);
        }
    }

    /// <summary>The caret — the moving end of the selection.</summary>
    public CodePosition Caret => _selection.Focus;

    public CodeHighlighter Highlighter { get; }
    public CodeHistory History { get; } = new();
    public CodeLanguageRules Rules => Highlighter.Language.Rules;

    /// <summary>Whether edits are refused — a viewer, a diff pane, a running debugger.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// Raised after every change, with the edit that caused it — null when the whole document was
    /// replaced. THE event an IDE builds on.
    /// </summary>
    public event Action<CodeEdit?>? Changed;

    /// <summary>Raised when the caret or selection moved, however it moved.</summary>
    public event Action<CodeRange>? SelectionChanged;

    /// <summary>
    /// The column a vertical move AIMS at. Moving down through a short line and on to a long one
    /// returns to the column you started from — losing it on the short line is the classic bug,
    /// and remembering it is why this field exists.
    /// </summary>
    private int _desiredColumn = -1;

    // ---- editing ------------------------------------------------------------------------------

    /// <summary>Replaces a range with text — the primitive every other edit is written in, and the
    /// one an IDE calls to apply a refactor, a formatter or a language server's edit.</summary>
    public bool Apply(CodeRange range, string text)
    {
        if (ReadOnly) return false;

        var ordered = new CodeRange(_document.Clamp(range.Start), _document.Clamp(range.End));
        var removed = _document.TextIn(ordered);
        if (removed.Length == 0 && text.Length == 0) return false;

        var before = _selection;
        var next = _document.Replace(ordered, text, out var caret);
        var line = ordered.Start.Line;
        var linesRemoved = ordered.End.Line - ordered.Start.Line;
        var linesInserted = caret.Line - ordered.Start.Line;

        _document = next;
        _selection = new CodeRange(caret);
        var edit = new CodeEdit(ordered, removed, text, before, _selection);
        History.Record(edit);
        Highlighter.LineChanged(_document, line, linesInserted, linesRemoved);

        Changed?.Invoke(edit);
        SelectionChanged?.Invoke(_selection);
        _desiredColumn = -1;
        return true;
    }

    /// <summary>Types text at the caret, replacing the selection — including a paste.</summary>
    public bool Insert(string text) => Apply(_selection, text);

    /// <summary>
    /// Types one character, with the pairing behaviours a code editor is expected to have: an
    /// opening bracket auto-closes, a closing bracket typed over its own auto-inserted twin steps
    /// over it instead of doubling it, and a quote around a selection wraps rather than replaces.
    /// </summary>
    public bool Type(char c)
    {
        if (ReadOnly) return false;
        CodeLanguageRules rules = Rules;

        // Wrapping a selection in a pair keeps the selection — that is the point of doing it.
        if (!_selection.IsEmpty)
        {
            foreach (var (open, close) in rules.Brackets)
            {
                if (c != open) continue;
                var text = _document.TextIn(_selection);
                return Apply(_selection, open + text + close);
            }
            foreach (var quote in rules.Quotes)
            {
                if (c != quote) continue;
                var text = _document.TextIn(_selection);
                return Apply(_selection, quote + text + quote);
            }
        }

        var line = _document.Line(Caret.Line);
        var after = Caret.Column < line.Length ? line[Caret.Column] : '\0';

        // Typing the closing half over the one that was auto-inserted just steps over it.
        foreach (var (_, close) in rules.Brackets)
        {
            if (c == close && after == close)
            {
                Selection = new CodeRange(Caret with { Column = Caret.Column + 1 });
                return true;
            }
        }
        foreach (var quote in rules.Quotes)
        {
            if (c == quote && after == quote)
            {
                Selection = new CodeRange(Caret with { Column = Caret.Column + 1 });
                return true;
            }
        }

        // Auto-close, but only where a closing character would not be in the way of real text.
        foreach (var (open, close) in rules.Brackets)
        {
            if (c != open) continue;
            if (after == '\0' || char.IsWhiteSpace(after) || rules.Brackets.Any(p => p.Close == after))
            {
                if (!Apply(_selection, $"{open}{close}")) return false;
                Selection = new CodeRange(Caret with { Column = Caret.Column - 1 });
                return true;
            }
        }
        foreach (var quote in rules.Quotes)
        {
            if (c != quote) continue;
            var before = Caret.Column > 0 ? line[Caret.Column - 1] : '\0';
            // Never inside a word: an apostrophe in `don't` is not an opening quote.
            if (CodeDocument.IsWordChar(before) || CodeDocument.IsWordChar(after)) break;
            if (after == '\0' || char.IsWhiteSpace(after))
            {
                if (!Apply(_selection, $"{quote}{quote}")) return false;
                Selection = new CodeRange(Caret with { Column = Caret.Column - 1 });
                return true;
            }
        }

        return Apply(_selection, c.ToString());
    }

    /// <summary>
    /// Enter, with the indentation a reader expects: the new line inherits the current one's, and
    /// gains a level when the line ends on something that opens one. Between a pair — the caret
    /// sitting inside <c>{}</c> — it opens the block and puts the closing brace on its own line.
    /// </summary>
    public bool InsertNewLine()
    {
        if (ReadOnly) return false;
        CodeLanguageRules rules = Rules;
        var line = _document.Line(Caret.Line);
        var indent = _document.IndentOf(Caret.Line);
        var step = rules.InsertSpaces ? new string(' ', rules.IndentWidth) : "\t";

        var beforeCaret = line[..Math.Min(Caret.Column, line.Length)].TrimEnd();
        var afterCaret = Caret.Column < line.Length ? line[Caret.Column..].TrimStart() : string.Empty;

        var opens = beforeCaret.Length > 0 && rules.IndentAfter.Contains(beforeCaret[^1]);
        var closesNext = afterCaret.Length > 0 && rules.OutdentOn.Contains(afterCaret[0]);

        if (opens && closesNext)
        {
            // Between the pair: an empty indented line, and the closer drops below it.
            if (!Apply(_selection, $"\n{indent}{step}\n{indent}")) return false;
            Selection = new CodeRange(new CodePosition(Caret.Line - 1, indent.Length + step.Length));
            return true;
        }
        return Apply(_selection, "\n" + indent + (opens ? step : string.Empty));
    }

    /// <summary>Backspace: the selection if there is one, else the character before the caret —
    /// and a whole indent step when the caret sits in leading whitespace, because that is what put
    /// it there.</summary>
    public bool DeleteBackward(CodeMotion motion = CodeMotion.Character)
    {
        if (ReadOnly) return false;
        if (!_selection.IsEmpty) return Apply(_selection, string.Empty);

        if (motion == CodeMotion.Word)
        {
            var start = MoveTo(Caret, CodeMotion.Word, CodeDirection.Backward);
            return Apply(new CodeRange(start, Caret), string.Empty);
        }

        var line = _document.Line(Caret.Line);
        var indent = _document.IndentOf(Caret.Line).Length;
        if (Caret.Column > 0 && Caret.Column <= indent && Rules.InsertSpaces)
        {
            var width = Rules.IndentWidth;
            var back = Caret.Column % width == 0 ? width : Caret.Column % width;
            return Apply(new CodeRange(Caret with { Column = Caret.Column - back }, Caret), string.Empty);
        }

        // Deleting the opening half of an auto-inserted pair takes the closing half with it.
        if (Caret.Column > 0 && Caret.Column < line.Length)
        {
            var before = line[Caret.Column - 1];
            var after = line[Caret.Column];
            var paired = Rules.Brackets.Any(p => p.Open == before && p.Close == after)
                || (Rules.Quotes.Contains(before) && before == after);
            if (paired)
            {
                return Apply(new CodeRange(Caret with { Column = Caret.Column - 1 },
                    Caret with { Column = Caret.Column + 1 }), string.Empty);
            }
        }

        var previous = _document.Previous(Caret);
        return previous != Caret && Apply(new CodeRange(previous, Caret), string.Empty);
    }

    /// <summary>Delete forward — the key labelled Delete on a full keyboard, fn+Backspace on a Mac.</summary>
    public bool DeleteForward(CodeMotion motion = CodeMotion.Character)
    {
        if (ReadOnly) return false;
        if (!_selection.IsEmpty) return Apply(_selection, string.Empty);

        var to = motion == CodeMotion.Word
            ? MoveTo(Caret, CodeMotion.Word, CodeDirection.Forward)
            : _document.Next(Caret);
        return to != Caret && Apply(new CodeRange(Caret, to), string.Empty);
    }

    // ---- indentation and comments -------------------------------------------------------------

    /// <summary>
    /// Tab. With a selection it indents every line it touches (that is what Tab means in an
    /// editor); with a caret it inserts one step — to the NEXT tab stop, not a fixed number of
    /// spaces, so a column stays a column.
    /// </summary>
    public bool Indent()
    {
        if (ReadOnly) return false;
        if (_selection.IsEmpty)
        {
            if (!Rules.InsertSpaces) return Apply(_selection, "\t");
            var width = Rules.IndentWidth;
            return Apply(_selection, new string(' ', width - Caret.Column % width));
        }
        return ShiftLines(add: true);
    }

    /// <summary>Shift+Tab: takes one step off every selected line, and off the caret's line when
    /// there is no selection.</summary>
    public bool Outdent() => !ReadOnly && ShiftLines(add: false);

    private bool ShiftLines(bool add)
    {
        var step = Rules.InsertSpaces ? new string(' ', Rules.IndentWidth) : "\t";
        var first = _selection.Start.Line;
        var last = _selection.End.Line;
        // A selection that ends at column 0 does not include that line — the same rule every
        // editor uses, and the reason shift+down then Tab does not indent one line too many.
        if (last > first && _selection.End.Column == 0) last--;

        var lines = new List<string>();
        for (var line = first; line <= last; line++)
        {
            var text = _document.Line(line);
            if (add) lines.Add(text.Length == 0 ? text : step + text);
            else if (text.StartsWith(step, StringComparison.Ordinal)) lines.Add(text[step.Length..]);
            else lines.Add(text.TrimStart(' ', '\t').Length == text.Length ? text : text[1..]);
        }

        var range = new CodeRange(new CodePosition(first, 0),
            new CodePosition(last, _document.Line(last).Length));
        var anchorShift = add ? step.Length : -(int)Math.Min(step.Length, _document.IndentOf(first).Length);
        if (!Apply(range, string.Join("\n", lines))) return false;

        Selection = new CodeRange(
            new CodePosition(first, Math.Max(0, _selection.Anchor.Column + anchorShift)),
            new CodePosition(last, _document.Line(last).Length));
        return true;
    }

    /// <summary>
    /// ⌘/ — comments the selected lines, or un-comments them when they are ALL commented already.
    /// A language with no line comment does nothing, which is the only correct answer for JSON.
    /// </summary>
    public bool ToggleLineComment()
    {
        if (ReadOnly || Rules.LineComment is not { } marker) return false;

        var first = _selection.Start.Line;
        var last = _selection.End.Line;
        if (last > first && _selection.End.Column == 0) last--;

        var allCommented = true;
        for (var line = first; line <= last; line++)
        {
            var text = _document.Line(line).TrimStart();
            if (text.Length == 0) continue;
            if (!text.StartsWith(marker, StringComparison.Ordinal)) { allCommented = false; break; }
        }

        var lines = new List<string>();
        for (var line = first; line <= last; line++)
        {
            var text = _document.Line(line);
            if (allCommented)
            {
                var at = text.IndexOf(marker, StringComparison.Ordinal);
                if (at < 0) { lines.Add(text); continue; }
                var after = at + marker.Length;
                if (after < text.Length && text[after] == ' ') after++;
                lines.Add(text[..at] + text[after..]);
            }
            else
            {
                var indent = _document.IndentOf(line);
                lines.Add(text.Length == 0 ? marker + " " : indent + marker + " " + text[indent.Length..]);
            }
        }

        var range = new CodeRange(new CodePosition(first, 0),
            new CodePosition(last, _document.Line(last).Length));
        return Apply(range, string.Join("\n", lines));
    }

    // ---- movement -----------------------------------------------------------------------------

    /// <summary>
    /// Moves the caret, optionally EXTENDING the selection — the one method behind every arrow,
    /// Home, End, PageUp and their shifted twins.
    /// </summary>
    public void Move(CodeMotion motion, CodeDirection direction, bool extend = false, int pageLines = 20)
    {
        // A plain arrow with a selection collapses to its edge rather than moving from the caret —
        // pressing → with text selected puts you after it, not one character further on.
        if (!extend && !_selection.IsEmpty && motion == CodeMotion.Character)
        {
            Selection = new CodeRange(direction == CodeDirection.Forward ? _selection.End : _selection.Start);
            return;
        }

        var target = MoveTo(Caret, motion, direction, pageLines);
        Selection = extend ? _selection with { Focus = target } : new CodeRange(target);
    }

    /// <summary>Where a movement LANDS, without moving anything — an IDE computing a jump.</summary>
    public CodePosition MoveTo(CodePosition from, CodeMotion motion, CodeDirection direction,
        int pageLines = 20)
    {
        var forward = direction == CodeDirection.Forward;
        switch (motion)
        {
            case CodeMotion.Character:
                _desiredColumn = -1;
                return forward ? _document.Next(from) : _document.Previous(from);

            case CodeMotion.Word:
                _desiredColumn = -1;
                return WordStep(from, forward);

            case CodeMotion.Line:
            {
                // The remembered column is what makes a run of ↓ through ragged lines come back
                // to where it started instead of collapsing to the shortest one.
                if (_desiredColumn < 0) _desiredColumn = from.Column;
                var line = Math.Clamp(from.Line + (forward ? 1 : -1), 0, _document.LineCount - 1);
                var column = Math.Min(_desiredColumn, _document.Line(line).Length);
                return new CodePosition(line, column);
            }

            case CodeMotion.Page:
            {
                if (_desiredColumn < 0) _desiredColumn = from.Column;
                var line = Math.Clamp(from.Line + (forward ? pageLines : -pageLines),
                    0, _document.LineCount - 1);
                return new CodePosition(line, Math.Min(_desiredColumn, _document.Line(line).Length));
            }

            case CodeMotion.LineBoundary:
                _desiredColumn = -1;
                return forward ? _document.LineEnd(from) : _document.LineStart(from);

            default:
                _desiredColumn = -1;
                return forward ? _document.End : CodePosition.Start;
        }
    }

    private CodePosition WordStep(CodePosition from, bool forward)
    {
        var here = _document.Clamp(from);
        var line = _document.Line(here.Line);

        if (forward)
        {
            if (here.Column >= line.Length) return _document.Next(here);
            var i = here.Column;
            // Skip what we are on, then the whitespace after it — one press lands on the next word.
            if (CodeDocument.IsWordChar(line[i]))
                while (i < line.Length && CodeDocument.IsWordChar(line[i])) i++;
            else if (!char.IsWhiteSpace(line[i]))
                while (i < line.Length && !CodeDocument.IsWordChar(line[i])
                       && !char.IsWhiteSpace(line[i])) i++;
            while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
            return here with { Column = i };
        }

        if (here.Column == 0) return _document.Previous(here);
        var back = here.Column;
        while (back > 0 && char.IsWhiteSpace(line[back - 1])) back--;
        if (back > 0 && CodeDocument.IsWordChar(line[back - 1]))
            while (back > 0 && CodeDocument.IsWordChar(line[back - 1])) back--;
        else
            while (back > 0 && !CodeDocument.IsWordChar(line[back - 1])
                   && !char.IsWhiteSpace(line[back - 1])) back--;
        return here with { Column = back };
    }

    // ---- selection ----------------------------------------------------------------------------

    public void SelectAll() => Selection = new CodeRange(CodePosition.Start, _document.End);

    /// <summary>The word under a position — a double click.</summary>
    public void SelectWord(CodePosition at) => Selection = _document.WordAt(at);

    /// <summary>The whole line — a triple click.</summary>
    public void SelectLine(int line)
    {
        var last = Math.Clamp(line, 0, _document.LineCount - 1);
        Selection = new CodeRange(new CodePosition(last, 0),
            last + 1 < _document.LineCount
                ? new CodePosition(last + 1, 0)
                : new CodePosition(last, _document.Line(last).Length));
    }

    /// <summary>What a copy puts on the clipboard: the selection, or the whole line when there is
    /// none — copying with nothing selected takes the line, in every editor worth using.</summary>
    public string CopyText() => _selection.IsEmpty
        ? _document.Line(Caret.Line) + "\n"
        : _document.TextIn(_selection);

    /// <summary>Cut is copy, then delete — and with no selection it takes the whole line.</summary>
    public string Cut()
    {
        var text = CopyText();
        if (_selection.IsEmpty) SelectLine(Caret.Line);
        Apply(_selection, string.Empty);
        return text;
    }

    // ---- history ------------------------------------------------------------------------------

    public bool Undo()
    {
        if (ReadOnly) return false;
        var next = History.Undo(_document, out var selection);
        if (next is null) return false;
        _document = next;
        _selection = new CodeRange(next.Clamp(selection.Anchor), next.Clamp(selection.Focus));
        Highlighter.Invalidate();
        Changed?.Invoke(null);
        SelectionChanged?.Invoke(_selection);
        return true;
    }

    public bool Redo()
    {
        if (ReadOnly) return false;
        var next = History.Redo(_document, out var selection);
        if (next is null) return false;
        _document = next;
        _selection = new CodeRange(next.Clamp(selection.Anchor), next.Clamp(selection.Focus));
        Highlighter.Invalidate();
        Changed?.Invoke(null);
        SelectionChanged?.Invoke(_selection);
        return true;
    }

    // ---- finding ------------------------------------------------------------------------------

    /// <summary>
    /// Every match of <paramref name="needle"/> — what a find bar highlights and steps through.
    /// Plain text, not a regular expression: an IDE that wants regex brings its own matcher and
    /// hands the ranges back as decorations.
    /// </summary>
    public IReadOnlyList<CodeRange> FindAll(string needle, bool matchCase = false)
    {
        var matches = new List<CodeRange>();
        if (needle.Length == 0) return matches;
        // Case folded into the STRINGS rather than carried as a comparison flag: the flag is a .NET
        // concept with no twin in `indexOf`, so a case-insensitive search would have quietly become
        // a case-sensitive one on the web half. Folding means the same thing on both.
        var pin = matchCase ? needle : needle.ToLowerInvariant();

        for (var line = 0; line < _document.LineCount; line++)
        {
            var raw = _document.Line(line);
            var text = matchCase ? raw : raw.ToLowerInvariant();
            var at = text.IndexOf(pin, StringComparison.Ordinal);
            while (at >= 0)
            {
                matches.Add(new CodeRange(new CodePosition(line, at),
                    new CodePosition(line, at + needle.Length)));
                at = at + pin.Length <= text.Length
                    ? text.IndexOf(pin, at + pin.Length, StringComparison.Ordinal)
                    : -1;
            }
        }
        return matches;
    }

    /// <summary>The match AFTER the caret, wrapping to the top — the Enter of a find bar.</summary>
    public CodeRange? FindNext(string needle, bool matchCase = false, bool backward = false)
    {
        var matches = FindAll(needle, matchCase);
        if (matches.Count == 0) return null;

        if (backward)
        {
            for (var i = matches.Count - 1; i >= 0; i--)
                if (matches[i].End <= _selection.Start) return matches[i];
            return matches[^1];
        }
        foreach (var match in matches)
            if (match.Start >= _selection.End) return match;
        return matches[0];
    }

    /// <summary>
    /// The bracket that PAIRS with the one at a position, or null — what draws the outline around
    /// a matching brace and what ⌘⇧\ jumps to. Counts nesting, so it lands on the right one.
    /// </summary>
    /// <summary>
    /// The bracket pair the CARET is against — the one an editor outlines. A caret sits BETWEEN
    /// characters, so it belongs to the bracket on either side of it, and the one behind wins
    /// (having just typed `)`, that is the one you mean).
    /// </summary>
    public (CodePosition Here, CodePosition There)? BracketAtCaret()
    {
        var caret = Caret;
        if (caret.Column > 0)
        {
            var behind = caret with { Column = caret.Column - 1 };
            if (MatchingBracket(behind) is { } match) return (behind, match);
        }
        return MatchingBracket(caret) is { } ahead ? (caret, ahead) : null;
    }

    public CodePosition? MatchingBracket(CodePosition at)
    {
        var here = _document.Clamp(at);
        var line = _document.Line(here.Line);
        if (here.Column >= line.Length) return null;
        var c = line[here.Column];

        foreach (var (open, close) in Rules.Brackets)
        {
            if (c == open) return ScanForBracket(here, open, close, forward: true);
            if (c == close) return ScanForBracket(here, close, open, forward: false);
        }
        return null;
    }

    private CodePosition? ScanForBracket(CodePosition from, char same, char other, bool forward)
    {
        var depth = 0;
        var position = from;
        while (true)
        {
            var line = _document.Line(position.Line);
            if (position.Column < line.Length)
            {
                var c = line[position.Column];
                if (c == same) depth++;
                else if (c == other)
                {
                    depth--;
                    if (depth == 0) return position;
                }
            }

            var next = forward ? _document.Next(position) : _document.Previous(position);
            if (next == position) return null;
            position = next;
        }
    }
}
