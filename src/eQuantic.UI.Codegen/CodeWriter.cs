using System.Text;

namespace eQuantic.UI.Codegen;

/// <summary>How a generated file indents. The file's own convention, not ours.</summary>
public enum IndentationType
{
    /// <summary>Four spaces per level — C#, TypeScript, JSON.</summary>
    Spaces,

    /// <summary>One tab per level — what Apple's property lists are written with.</summary>
    Tabs,
}

/// <summary>
/// The indentation engine every generated file is written through. Nothing about it knows the
/// language it is producing: it knows a line, a scope and how deep we currently are.
/// <para>
/// Scopes are DISPOSABLE, which is the whole reason this exists. Generated output goes wrong in one
/// characteristic way — an opener written and its closer forgotten, or written at the wrong depth —
/// and a <c>using</c> makes that unrepresentable. The closing token travels with the scope, so a
/// writer for a language of braces and one for a language of tags share this class unchanged.
/// </para>
/// </summary>
public class CodeWriter
{
    private readonly StringBuilder _content = new();
    private readonly Stack<string?> _closers = new();
    private readonly ScopeTracker _tracker;

    public CodeWriter(int indentLevel = 0, IndentationType indentationType = IndentationType.Spaces)
    {
        IndentLevel = indentLevel;
        IndentationType = indentationType;
        // One tracker, reused: what a scope has to close is on the stack, not on the tracker.
        _tracker = new ScopeTracker(this);
    }

    public int IndentLevel { get; set; }

    public IndentationType IndentationType { get; private set; }

    /// <summary>Lines written so far, 1-based — what a source map counts.</summary>
    public int CurrentLine { get; private set; } = 1;

    /// <summary>Appends to the CURRENT line: no indentation, no line break.</summary>
    public CodeWriter Append(string text)
    {
        _content.Append(text);
        CurrentLine += NewlinesIn(text);
        return this;
    }

    /// <summary>
    /// The line break every writer here emits: LF, on every host. What this writes is read by
    /// OTHER platforms — a plist by macOS, TypeScript by bun, a manifest by Android — and a
    /// generated file that changes its line endings with the machine that generated it is a diff
    /// nobody asked for and a test that passes on one desk and fails on the next.
    /// <c>StringBuilder.AppendLine</c> would write CRLF on Windows.
    /// </summary>
    private const char LineBreak = '\n';

    /// <summary>Appends a whole line at the current depth.</summary>
    public CodeWriter AppendLine(string line)
    {
        _content.Append(Indentation()).Append(line).Append(LineBreak);
        CurrentLine += NewlinesIn(line) + 1;
        return this;
    }

    /// <summary>
    /// What the text ITSELF advances the line counter by. <see cref="CurrentLine"/> is what a source
    /// map anchors against, and a writer is routinely handed a block that is already several lines —
    /// a converted method body, a doc comment. Counting those as one line silently shifts every
    /// mapping after them, which reads in a debugger as breakpoints landing on the wrong statement.
    /// Counting '\n' covers both line endings: "\r\n" has exactly one.
    /// </summary>
    private static int NewlinesIn(string text)
    {
        var count = 0;
        foreach (var c in text)
        {
            if (c == '\n') count++;
        }
        return count;
    }

    /// <summary>A blank line carries no indentation — trailing whitespace is a diff nobody wants.</summary>
    public CodeWriter AppendLine()
    {
        _content.Append(LineBreak);
        CurrentLine++;
        return this;
    }

    /// <summary>Opens the current line at the right depth, for output assembled in pieces.</summary>
    public CodeWriter StartLine()
    {
        _content.Append(Indentation());
        return this;
    }

    public CodeWriter EndLine()
    {
        _content.Append(LineBreak);
        CurrentLine++;
        return this;
    }

    /// <summary>
    /// Opens a scope: writes <paramref name="opener"/>, indents, and closes with
    /// <paramref name="closer"/> when the returned handle is disposed. A null closer is a scope that
    /// only indents — a JSON member's value, a plist key's body.
    /// </summary>
    public IDisposable BeginScope(string opener = "{", string? closer = "}")
    {
        AppendLine(opener);
        _closers.Push(closer);
        IndentLevel++;
        return _tracker;
    }

    /// <summary>Same, with a line written BEFORE the opener — <c>class Foo</c> then <c>{</c>.</summary>
    public IDisposable BeginScope(string line, string opener, string? closer)
    {
        AppendLine(line);
        return BeginScope(opener, closer);
    }

    /// <summary>
    /// A block whose opener is already at the END of a line — <c>class Foo {</c>, the brace style
    /// most languages are written in. The closer still travels with the scope, which is the only
    /// part that ever goes wrong.
    /// </summary>
    public IDisposable BeginBlock(string lineEndingInOpener, string closer = "}")
    {
        AppendLine(lineEndingInOpener);
        _closers.Push(closer);
        IndentLevel++;
        return _tracker;
    }

    public void EndScope()
    {
        IndentLevel = Math.Max(0, IndentLevel - 1);
        if (_closers.Count > 0 && _closers.Pop() is { } closer) AppendLine(closer);
    }

    public void SetIndentationType(IndentationType indentationType) => IndentationType = indentationType;

    public override string ToString() => _content.ToString();

    private string Indentation() => IndentationType == IndentationType.Tabs
        ? new string('\t', Math.Max(0, IndentLevel))
        : new string(' ', Math.Max(0, IndentLevel) * 4);

    private sealed class ScopeTracker(CodeWriter parent) : IDisposable
    {
        public void Dispose() => parent.EndScope();
    }
}
