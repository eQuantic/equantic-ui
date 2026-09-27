using System.Text;

namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>
/// The builder both writers compose with: text appended piece by piece, and the marks of every
/// piece moved to where it landed. A mark on a piece's first line moves by the column the piece
/// started at; one further down keeps its column, since it begins a line of its own.
/// </summary>
internal sealed class JsWrittenBuilder
{
    private readonly StringBuilder _text = new();
    private readonly List<JsLineMark> _marks = [];
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
        foreach (var mark in piece.Marks)
            _marks.Add(mark with { Line = _line + mark.Line, Column = mark.Line == 0 ? _column + mark.Column : mark.Column });
        return Add(piece.Text);
    }

    public JsWrittenBuilder AddIf(bool condition, Func<JsWritten> piece) => condition ? Add(piece()) : this;

    public JsWritten Done() => new(_text.ToString(), _marks);

    private void Advance(string piece)
    {
        var lastBreak = piece.LastIndexOf('\n');
        if (lastBreak < 0)
        {
            _column += piece.Length;
            return;
        }
        _line += piece.Count(c => c == '\n');
        _column = piece.Length - lastBreak - 1;
    }
}
