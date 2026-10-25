using eQuantic.UI.Code;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;

namespace eQuantic.UI.Spreadsheets;

/// <summary>
/// Excel's formula bar, a component the app places beside its grid over the same controller: at rest the
/// active cell's value, and while a cell is edited the entry itself, the same document the cell's editor
/// shows, so the two agree after every keystroke, composition and undo. A press in it edits the active cell
/// from here, in Edit mode, with the caret where it landed; Enter, Tab and Escape do what they do in the
/// cell and hand the keyboard back to the grid. A line wider than the bar scrolls, and the caret is kept in
/// view as a code editor keeps its own.
/// </summary>
public sealed class FormulaBar : StatefulComponent
{
    /// <summary>What the bar keeps between its border and its text.</summary>
    public static EdgeInsets Padding => EdgeInsets.Symmetric(8, 5);

    private const TypeRole BarType = TypeRole.BodyM;

    private FormulaBar _config;

    /// <summary>What a refresh of the controller does here: build again, as one delegate.</summary>
    private readonly Action _rebuild;
    private SheetController? _listening;

    /// <summary>How wide the bar's viewport turned out to be, handed back by its scroll view: the text is
    /// never narrower than the room you can click in.</summary>
    private float _viewportWidth;

    public FormulaBar(SheetController controller)
    {
        Controller = controller;
        _config = this;
        _rebuild = () => SetState(() => { });
    }

    public SheetController Controller { get; private set; }

    /// <summary>How wide the bar is; the room it is given by default.</summary>
    public SizeValue Width { get; init; } = SizeValue.Fill;

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not FormulaBar fresh) return;
        if (_listening is not null) Listen(fresh.Controller);
        Controller = fresh.Controller;
        _config = fresh;
    }

    protected override void OnMount() => Listen(Controller);

    protected override void OnUnmount() => Listen(null);

    /// <summary>Listens to <paramref name="controller"/>'s refreshes, and to no other: a key in the grid
    /// changes what the bar shows.</summary>
    private void Listen(SheetController? controller)
    {
        if (ReferenceEquals(_listening, controller)) return;
        if (_listening is not null) _listening.Refreshed -= _rebuild;
        _listening = controller;
        if (controller is not null) controller.Refreshed += _rebuild;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var palette = SheetPalette.Of(theme);
        var type = theme.Type(BarType);
        var padding = Padding;
        Func<string, float> measure = text => context.MeasureText(text, type);
        var bar = Controller.FormulaBar;
        // THE geometry the entry is read on while the bar holds its caret, handed over as it builds.
        bar.Grid = new CodeGrid(new Point(padding.Start, padding.Top),
            new Size(Math.Max(measure(" "), 4f), type.LineHeight), Measure: measure);

        var editing = Controller.Editing;
        var lines = editing
            ? Lines(Controller.Entry.Document)
            : [.. Controller.Document.GetCell(Controller.ActiveCell).Split('\n')];
        var widest = 0f;
        foreach (var line in lines) widest = Math.Max(widest, measure(line));
        var height = Math.Max(1, lines.Count) * type.LineHeight + padding.Top + padding.Bottom;

        VisualNode text = editing
            ? new EntryLines(Controller.Entry, bar.Grid, Point.Zero, BarType, palette, active: bar.HoldsTheCaret)
            : AtRest(lines, padding, type, palette);
        var content = new Box(new BoxStyle
        {
            // As wide as the text and the room past it, so a long line scrolls, and never narrower than the
            // viewport, so a press anywhere in the bar lands in it, an empty one's included.
            Width = SizeValue.Fixed(MathF.Max(_viewportWidth,
                MathF.Ceiling(widest) + padding.Start + padding.End + CodeEditorController.CaretWidth)),
            MinWidth = 0,
            Height = SizeValue.Fixed(height),
        }, text);
        var surface = new CodeSurface(content, bar)
        {
            OnChanged = () => Controller.Refresh(),
            Label = SdkStrings.FormulaBar,
            CaretColor = palette.CellText,
        };
        return new Box(new BoxStyle
        {
            Width = _config.Width,
            Height = SizeValue.Fixed(height + 2),
            Background = palette.CellBackground,
            BorderColor = palette.Gridline,
            BorderWidth = 1f,
            Clip = true,
        }, new ScrollView(surface, ScrollAxis.Horizontal)
        {
            Width = SizeValue.Fill,
            OnViewportChanged = width =>
            {
                if (MathF.Abs(width - _viewportWidth) < 1) return;
                SetState(() => _viewportWidth = width);
            },
        });
    }

    private static List<string> Lines(CodeDocument document)
    {
        var lines = new List<string>(document.LineCount);
        for (var line = 0; line < document.LineCount; line++) lines.Add(document.Line(line));
        return lines;
    }

    /// <summary>The active cell's value, a line under the other, where the entry's lines would stand.</summary>
    private static VisualNode AtRest(List<string> lines, EdgeInsets padding, TypeStyle type, SheetPalette palette)
    {
        var column = new Column(gap: 0);
        foreach (var line in lines)
        {
            column.Add(new Box(new BoxStyle { Height = SizeValue.Fixed(type.LineHeight) },
                line.Length == 0 ? null : new Text(line, BarType, palette.CellText, maxLines: 1)));
        }
        return new Box(new BoxStyle { Padding = padding }, column);
    }
}
