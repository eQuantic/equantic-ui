using eQuantic.UI.Code;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;

namespace eQuantic.UI.Spreadsheets;

/// <summary>
/// Excel's name box, a component the app places beside its grid over the same controller: the active cell's
/// address, or the size of the range a drag is drawing (<c>5R x 3C</c>, in the app's language), and a way to
/// go somewhere. A press takes the keyboard with the address selected; Enter goes to a cell, a range, rows
/// or columns and hands the keyboard back to the grid, or keeps a text that names nothing and marks the box
/// invalid; Escape puts the address back. A reference wider than the box scrolls, and the caret is kept in
/// view. F5 and Ctrl+G (⌃G on a Mac) bring the keyboard here from the grid while a name box is mounted.
/// </summary>
public sealed class NameBox : StatefulComponent
{
    /// <summary>What the box keeps between its border and its text.</summary>
    public static EdgeInsets Padding => EdgeInsets.Symmetric(8, 5);

    /// <summary>How wide the box is unless the app says otherwise: Excel's own.</summary>
    public const float DefaultWidth = 96f;

    private const TypeRole BoxType = TypeRole.BodyM;

    private NameBox _config;

    /// <summary>The controller this box is placed on and listens to while it is mounted, or null.</summary>
    private SheetController? _counted;

    /// <summary>What a refresh of the controller does here: build again, as one delegate.</summary>
    private readonly Action _rebuild;

    /// <summary>How wide the box's viewport turned out to be, handed back by its scroll view.</summary>
    private float _viewportWidth;

    public NameBox(SheetController controller)
    {
        Controller = controller;
        _config = this;
        _rebuild = () => SetState(() => { });
    }

    public SheetController Controller { get; private set; }

    /// <summary>How wide the box is.</summary>
    public SizeValue Width { get; init; } = SizeValue.Fixed(DefaultWidth);

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not NameBox fresh) return;
        if (_counted is not null) Place(fresh.Controller);
        Controller = fresh.Controller;
        _config = fresh;
    }

    protected override void OnMount() => Place(Controller);

    protected override void OnUnmount() => Place(null);

    /// <summary>Counts this box on <paramref name="controller"/>, so its Go To keys come here, and listens to
    /// its refreshes: a press in the grid changes the address the box shows.</summary>
    private void Place(SheetController? controller)
    {
        if (ReferenceEquals(_counted, controller)) return;
        if (_counted is not null)
        {
            _counted.NameBox.Removed();
            _counted.Refreshed -= _rebuild;
        }
        _counted = controller;
        if (controller is null) return;
        controller.NameBox.Placed();
        controller.Refreshed += _rebuild;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var palette = SheetPalette.Of(theme);
        var type = theme.Type(BoxType);
        var padding = Padding;
        var box = Controller.NameBox;
        Func<string, float> measure = text => context.MeasureText(text, type);
        box.Grid = new CodeGrid(new Point(padding.Start, padding.Top), new Size(4f, type.LineHeight), Measure: measure);

        var shown = box.Typing ? box.Entry.Document.Line(0) : Label();
        VisualNode text = box.Typing
            ? new EntryLines(box.Entry, box.Grid, Point.Zero, BoxType, palette, active: true)
            : new Box(new BoxStyle { Padding = padding },
                new Text(shown, BoxType, palette.CellText, maxLines: 1));
        var height = type.LineHeight + padding.Top + padding.Bottom;
        var surface = new CodeSurface(new Box(new BoxStyle
        {
            // As wide as the text and the caret past it, so a reference wider than the box scrolls and the
            // caret is kept in view, and never narrower than the box, so a press anywhere in it lands.
            Width = SizeValue.Fixed(MathF.Max(_viewportWidth,
                MathF.Ceiling(measure(shown)) + padding.Start + padding.End + CodeEditorController.CaretWidth)),
            Height = SizeValue.Fixed(height),
        }, text), box)
        {
            OnChanged = () => Controller.Refresh(),
            Label = SdkStrings.NameBox,
            CaretColor = palette.CellText,
        };
        return new Box(new BoxStyle
        {
            Width = _config.Width,
            Height = SizeValue.Fixed(height + 2),
            Background = palette.CellBackground,
            BorderColor = box.Invalid ? theme.Colors(Variant.Destructive).Base : palette.Gridline,
            BorderWidth = box.Invalid ? 2f : 1f,
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

    /// <summary>What the box shows at rest: the size of the range a drag is drawing, or the active cell.</summary>
    private string Label()
    {
        var selection = Controller.Selection;
        return Controller.Selecting
            ? SdkStrings.RangeSize(selection.RowCount, selection.ColCount)
            : Controller.ActiveCell.ToA1();
    }
}
