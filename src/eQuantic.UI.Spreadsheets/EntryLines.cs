using eQuantic.UI.Code;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Spreadsheets;

/// <summary>
/// An entry's lines, drawn in a proportional type on the grid the engine placed them on: the cell's
/// editor, the formula bar and the name box all draw their text with it. A line stands where its grid
/// says, relative to <c>corner</c>, the point of the grid's coordinates this component's top-left is at.
/// The view that holds the caret draws the selection's bands under the text and underlines a composition
/// in flight; the other draws the text alone, as Excel's inactive view does. The caret is the host's to
/// paint and blink.
/// </summary>
internal sealed class EntryLines : StatelessComponent
{
    private readonly CodeEditorController _entry;
    private readonly CodeGrid _grid;
    private readonly Point _corner;
    private readonly TypeRole _role;
    private readonly SheetPalette _palette;
    private readonly bool _active;

    public EntryLines(CodeEditorController entry, CodeGrid grid, Point corner, TypeRole role, SheetPalette palette,
        bool active)
    {
        _entry = entry;
        _grid = grid;
        _corner = corner;
        _role = role;
        _palette = palette;
        _active = active;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var document = _entry.Document;
        var lineHeight = _grid.Cell.Height;
        var layers = new Stack();
        // The stack takes its size from a box that draws nothing: the room it is given.
        layers.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill }));
        // The view that holds the caret reads the entry on its OWN grid: another view of the same entry
        // may have put its geometry on the engine since (the cell's editor builds beside the formula bar).
        if (_active) _entry.Grid = _grid;
        if (_active)
        {
            foreach (var band in _entry.SelectionBandsIn(0, document.LineCount - 1))
                layers.Add(Rectangle(band, _palette.EntrySelection));
        }
        for (var line = 0; line < document.LineCount; line++)
        {
            var text = document.Line(line);
            if (text.Length == 0) continue;
            var width = _grid.Measure is { } measure ? measure(text) : text.Length * _grid.Cell.Width;
            // As wide as its measure and a little more, so the line is never cut short and never wrapped:
            // the editor around it clips what goes past its edge.
            layers.Add(new Positioned(new Box(new BoxStyle
            {
                Width = SizeValue.Fixed(MathF.Ceiling(width) + 2),
                Height = SizeValue.Fixed(lineHeight),
            }, new Text(text, _role, _palette.CellText, maxLines: 1)),
                top: _grid.Origin.Y - _corner.Y + line * lineHeight,
                start: _grid.Origin.X - _corner.X));
        }
        if (_active && _entry.Composition is { } composing && !composing.IsEmpty)
        {
            // The composition's text is IN the document, marked by a line under it, as every platform's
            // own field marks the text an input method is still building.
            var from = _entry.CaretRect(composing.Start);
            var to = _entry.CaretRect(composing.End);
            if (from.Y == to.Y && to.X > from.X)
                layers.Add(Rectangle(new Rect(from.X, from.Bottom - 1.5f, to.X - from.X, 1.5f), _palette.CellText));
        }
        return layers;
    }

    private Positioned Rectangle(Rect rect, ColorToken colour) =>
        new(new Box(new BoxStyle
        {
            Width = SizeValue.Fixed(rect.Width),
            Height = SizeValue.Fixed(rect.Height),
            Background = colour,
        }), top: rect.Y - _corner.Y, start: rect.X - _corner.X);
}
