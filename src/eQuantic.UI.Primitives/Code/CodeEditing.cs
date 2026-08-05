namespace eQuantic.UI.Primitives;

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

/// <summary>
/// UNDO, as a person expects it: one press takes back the last THING, not the last character.
/// <para>
/// Runs of typing coalesce until something breaks the run — a newline, a deletion, a caret that
/// moved elsewhere, or a pause. Anything else (a paste, a formatting pass, a refactor) is its own
/// step because it arrived as one.
/// </para>
/// </summary>
public sealed class CodeHistory
{
    private readonly List<CodeEdit> _past = [];
    private readonly List<CodeEdit> _future = [];
    private CodePosition _runEnd = new(-1, -1);

    /// <summary>How many steps deep the history goes before the oldest is dropped.</summary>
    public int Limit { get; init; } = 500;

    public bool CanUndo => _past.Count > 0;
    public bool CanRedo => _future.Count > 0;

    /// <summary>Records an edit. A new edit always kills the redo branch — the future you did not
    /// take stops existing the moment you type something else, which is what every editor does.</summary>
    public void Record(CodeEdit edit)
    {
        _future.Clear();

        // Coalesce: a character typed right where the last one landed continues that run.
        if (edit.IsSimpleInsert && _past.Count > 0 && edit.Range.Start == _runEnd)
        {
            var previous = _past[^1];
            if (previous.IsSimpleInsert)
            {
                _past[^1] = previous with
                {
                    InsertedText = previous.InsertedText + edit.InsertedText,
                    SelectionAfter = edit.SelectionAfter,
                };
                _runEnd = edit.InsertedRange.End;
                return;
            }
        }

        _past.Add(edit);
        if (_past.Count > Limit) _past.RemoveAt(0);
        _runEnd = edit.IsSimpleInsert ? edit.InsertedRange.End : new CodePosition(-1, -1);
    }

    /// <summary>Ends the current typing run, so the NEXT character starts a new undo step. Called
    /// when the caret moves somewhere else, when the editor loses focus, when a file is saved.</summary>
    public void Break() => _runEnd = new CodePosition(-1, -1);

    /// <summary>Takes the last step back, applying it to <paramref name="document"/>.</summary>
    public CodeDocument? Undo(CodeDocument document, out CodeRange selection)
    {
        selection = default;
        if (_past.Count == 0) return null;

        var edit = _past[^1];
        _past.RemoveAt(_past.Count - 1);
        _future.Add(edit);
        Break();

        var next = document.Replace(edit.InsertedRange, edit.RemovedText, out _);
        selection = edit.SelectionBefore;
        return next;
    }

    /// <summary>Puts back what <see cref="Undo"/> took.</summary>
    public CodeDocument? Redo(CodeDocument document, out CodeRange selection)
    {
        selection = default;
        if (_future.Count == 0) return null;

        var edit = _future[^1];
        _future.RemoveAt(_future.Count - 1);
        _past.Add(edit);
        Break();

        var next = document.Replace(edit.Range, edit.InsertedText, out _);
        selection = edit.SelectionAfter;
        return next;
    }

    public void Clear()
    {
        _past.Clear();
        _future.Clear();
        Break();
    }
}

/// <summary>
/// What a language needs beyond colours, and what every editor behaviour is built from: how it
/// comments, which brackets pair, and what opens a new indentation level. An IDE author fills
/// these in for a new dialect and gets ⌘/, auto-indent, auto-closing and bracket matching without
/// writing any of them.
/// </summary>
public sealed record CodeLanguageRules
{
    public static readonly CodeLanguageRules Default = new();

    /// <summary>What starts a comment to end of line — <c>//</c>, <c>#</c>. Null = the language
    /// has none, and the comment command does nothing rather than corrupting the file.</summary>
    public string? LineComment { get; init; }

    /// <summary>The pair that wraps a block comment.</summary>
    public (string Open, string Close)? BlockComment { get; init; }

    /// <summary>Pairs that auto-close, match, and jump — the same table serves all three.</summary>
    public IReadOnlyList<(char Open, char Close)> Brackets { get; init; } =
        [('(', ')'), ('[', ']'), ('{', '}')];

    /// <summary>Quotes that auto-close around a caret (and never inside a word).</summary>
    public IReadOnlyList<char> Quotes { get; init; } = ['"', '\''];

    /// <summary>Characters that, at the end of a line, mean the NEXT line indents one level.</summary>
    public IReadOnlyList<char> IndentAfter { get; init; } = ['{', '(', '['];

    /// <summary>Characters that, typed at the head of a line, mean this line un-indents — a
    /// closing brace snapping back as you type it.</summary>
    public IReadOnlyList<char> OutdentOn { get; init; } = ['}', ')', ']'];

    /// <summary>How wide one indentation step is, in spaces.</summary>
    public int IndentWidth { get; init; } = 4;

    /// <summary>Whether Tab inserts spaces (the default) or a tab character.</summary>
    public bool InsertSpaces { get; init; } = true;
}

/// <summary>What a completion offers, and what accepting it does.</summary>
public sealed record CodeCompletionItem(string Label, CodeCompletionKind Kind = CodeCompletionKind.Text)
{
    /// <summary>What is actually inserted — defaults to the label.</summary>
    public string? InsertText { get; init; }

    /// <summary>The one-line explanation beside the label (a signature, a type).</summary>
    public string? Detail { get; init; }

    /// <summary>The longer text a details pane shows.</summary>
    public string? Documentation { get; init; }

    /// <summary>Sorts above the rest when set — a language server's own ranking.</summary>
    public string? SortText { get; init; }

    /// <summary>The range this replaces; null = the word being typed.</summary>
    public CodeRange? Replacing { get; init; }
}

/// <summary>The icon and colour a completion carries — the LSP set, trimmed to what a list shows.</summary>
public enum CodeCompletionKind : byte
{
    Text = 0, Method = 1, Function = 2, Constructor = 3, Field = 4, Variable = 5, Class = 6,
    Interface = 7, Module = 8, Property = 9, Enum = 10, Keyword = 11, Snippet = 12, File = 13,
}

/// <summary>
/// Where completions come from. Implemented by the APP — an IDE hands back what its language
/// server said; a form editor hands back column names. Asynchronous because the answer usually
/// crosses a process boundary, and an editor that blocks on it is an editor that stutters.
/// </summary>
public interface ICodeCompletionProvider
{
    /// <summary>Characters that OPEN the list unprompted — a dot, a slash inside a path.</summary>
    IReadOnlyList<char> TriggerCharacters => ['.'];

    Task<IReadOnlyList<CodeCompletionItem>> CompleteAsync(
        CodeDocument document, CodePosition position, CancellationToken cancellation);
}

/// <summary>What hovering over a range explains.</summary>
public sealed record CodeHover(CodeRange Range, string Text)
{
    /// <summary>A code-formatted first line: a signature, a resolved type.</summary>
    public string? Signature { get; init; }
}

/// <summary>Where hover text comes from — the same shape as completion, and for the same reason.</summary>
public interface ICodeHoverProvider
{
    Task<CodeHover?> HoverAsync(CodeDocument document, CodePosition position,
        CancellationToken cancellation);
}

/// <summary>How loud a diagnostic is — the four levels every tool already agrees on.</summary>
public enum CodeDiagnosticSeverity : byte { Hint = 0, Information = 1, Warning = 2, Error = 3 }

/// <summary>
/// A problem, where it is, and how bad — the squiggle under the code and the row in the problems
/// list are the same record. Diagnostics are DATA the app hands in, not something the editor
/// computes: the editor's job is to show them at the right place after every edit.
/// </summary>
public sealed record CodeDiagnostic(CodeRange Range, string Message,
    CodeDiagnosticSeverity Severity = CodeDiagnosticSeverity.Error)
{
    /// <summary>The rule that fired — "EQ2004", "CS0246" — shown beside the message.</summary>
    public string? Code { get; init; }

    /// <summary>Who said so: the compiler, the analyzer, the linter.</summary>
    public string? Source { get; init; }
}

/// <summary>What a decoration DOES to the range it covers.</summary>
public enum CodeDecorationKind : byte
{
    /// <summary>A background wash — a search match, a highlighted symbol.</summary>
    Highlight = 0,
    /// <summary>A wavy underline — a diagnostic, in the diagnostic's colour.</summary>
    Squiggle = 1,
    /// <summary>A box around the range — a matching bracket.</summary>
    Outline = 2,
    /// <summary>A strike-through — deleted in a diff, or unreachable code.</summary>
    Strike = 3,
}

/// <summary>
/// An extra mark over a range that the LANGUAGE did not put there: the other occurrences of the
/// symbol under the caret, the current search match, the matching bracket, a diff hunk. Handed in
/// as data so the same editor serves an IDE, a diff viewer and a log reader.
/// </summary>
public sealed record CodeDecoration(CodeRange Range, CodeDecorationKind Kind = CodeDecorationKind.Highlight)
{
    /// <summary>Overrides the theme's colour for this kind — a diff green, a diagnostic red.</summary>
    public ColorToken? Color { get; init; }
}

/// <summary>What the gutter shows beside a line, left of its number.</summary>
public enum CodeGutterKind : byte
{
    None = 0, Breakpoint = 1, BreakpointDisabled = 2, Error = 3, Warning = 4,
    Added = 5, Modified = 6, Removed = 7, CurrentStatement = 8,
}

/// <summary>A mark in the gutter — a breakpoint, a git change, the statement a debugger stopped
/// on. The line is where it sits; pressing it is an event the app answers.</summary>
public sealed record CodeGutterMarker(int Line, CodeGutterKind Kind)
{
    public string? Tooltip { get; init; }
}

/// <summary>A collapsible region: the lines it hides when folded.</summary>
public sealed record CodeFold(int StartLine, int EndLine)
{
    /// <summary>What the collapsed line shows instead — "{ … }", "#region Layout".</summary>
    public string? Placeholder { get; init; }
}

/// <summary>
/// Where folds come from. The default implementation folds by INDENTATION, which works for every
/// language including the ones with no braces — an app with a parser hands back better ones.
/// </summary>
public interface ICodeFoldProvider
{
    IReadOnlyList<CodeFold> FoldsFor(CodeDocument document);
}

/// <summary>
/// Folds derived from indentation alone: a line that is followed by more-indented lines opens a
/// region that ends where the indentation comes back. No parser, no language knowledge, and right
/// often enough to be the default.
/// </summary>
public sealed class IndentationFoldProvider : ICodeFoldProvider
{
    public IReadOnlyList<CodeFold> FoldsFor(CodeDocument document)
    {
        var folds = new List<CodeFold>();
        for (var line = 0; line < document.LineCount - 1; line++)
        {
            var text = document.Line(line);
            if (text.Trim().Length == 0) continue;

            var indent = document.IndentOf(line).Length;
            var last = line;
            for (var next = line + 1; next < document.LineCount; next++)
            {
                var candidate = document.Line(next);
                if (candidate.Trim().Length == 0) continue;          // blanks belong to the region
                if (document.IndentOf(next).Length <= indent) break;
                last = next;
            }
            if (last > line) folds.Add(new CodeFold(line, last));
        }
        return folds;
    }
}
