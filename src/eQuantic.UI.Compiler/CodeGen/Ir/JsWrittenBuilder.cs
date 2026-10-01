using System.Text;

namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>
/// The builder both writers compose with: text appended piece by piece, and the marks of every
/// piece moved to where it landed. A mark on a piece's first line moves by the column the piece
/// started at; one further down keeps its column, since it begins a line of its own. Only an
/// arrow's block ever carries a mark, so the lists are made when the first one arrives.
/// </summary>
internal sealed class JsWrittenBuilder
{
    private readonly StringBuilder _text = new();
    private List<JsLineMark>? _marks;
    private List<JsPosition>? _resumes;
    private int _line;
    private int _column;

    public JsWrittenBuilder Add(string piece)
    {
        _text.Append(piece);
        Advance(piece);
        return this;
    }

    public JsWrittenBuilder Add(JsWritten piece)
    {
        if (piece.Carries)
        {
            foreach (var mark in piece.Marks)
                (_marks ??= []).Add(mark with { Line = _line + mark.Line, Column = Moved(mark.Line, mark.Column) });
            foreach (var at in piece.Resumes)
                (_resumes ??= []).Add(new JsPosition(_line + at.Line, Moved(at.Line, at.Column)));
        }
        return Add(piece.Text);
    }

    public JsWrittenBuilder AddIf(bool condition, Func<JsWritten> piece) => condition ? Add(piece()) : this;

    /// <summary>The pieces with <paramref name="separator"/> between each two.</summary>
    public JsWrittenBuilder AddJoined(string separator, IEnumerable<JsWritten> pieces)
    {
        var first = true;
        foreach (var piece in pieces)
        {
            if (!first) Add(separator);
            Add(piece);
            first = false;
        }
        return this;
    }

    /// <summary>Marks where the text has reached as a place the statement around it resumes.</summary>
    public JsWrittenBuilder Resume()
    {
        (_resumes ??= []).Add(new JsPosition(_line, _column));
        return this;
    }

    public JsWritten Done() =>
        _marks is null && _resumes is null
            ? JsWritten.Of(_text.ToString())
            : new JsWritten(_text.ToString(), _marks?.ToArray() ?? [], _resumes?.ToArray() ?? []);

    private int Moved(int line, int column) => line == 0 ? _column + column : column;

    private void Advance(string piece)
    {
        var lastBreak = piece.LastIndexOf('\n');
        if (lastBreak < 0)
        {
            _column += piece.Length;
            return;
        }
        _line += piece.AsSpan().Count('\n');
        _column = piece.Length - lastBreak - 1;
    }
}
