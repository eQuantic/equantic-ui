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
/// invalid; Escape puts the address back. F5 and Ctrl+G (⌃G on a Mac) bring the keyboard here from the grid
/// while a name box is mounted.
/// </summary>
public sealed class NameBox : StatefulComponent
{
    /// <summary>What the box keeps between its border and its text.</summary>
    public static EdgeInsets Padding => EdgeInsets.Symmetric(8, 5);

    /// <summary>How wide the box is unless the app says otherwise: Excel's own.</summary>
    public const float DefaultWidth = 96f;

    private const TypeRole BoxType = TypeRole.BodyM;

    private NameBox _config;
    private SheetController? _counted;

    public NameBox(SheetController controller)
    {
        Controller = controller;
        _config = this;
    }

    public SheetController Controller { get; private set; }

    /// <summary>How wide the box is.</summary>
    public SizeValue Width { get; init; } = SizeValue.Fixed(DefaultWidth);

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not NameBox fresh) return;
        if (!ReferenceEquals(fresh.Controller, Controller) && _counted is not null)
        {
            _counted.NameBox.Removed();
            fresh.Controller.NameBox.Placed();
            _counted = fresh.Controller;
        }
        Controller = fresh.Controller;
        _config = fresh;
    }

    protected override void OnMount()
    {
        Controller.NameBox.Placed();
        _counted = Controller;
    }

    protected override void OnUnmount()
    {
        _counted?.NameBox.Removed();
        _counted = null;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var palette = SheetPalette.Of(theme);
        var type = theme.Type(BoxType);
        var padding = Padding;
        var box = Controller.NameBox;
        box.Grid = new CodeGrid(new Point(padding.Start, padding.Top),
            new Size(4f, type.LineHeight), Measure: text => context.MeasureText(text, type));

        VisualNode text = box.Typing
            ? new EntryLines(box.Entry, box.Grid, Point.Zero, BoxType, palette, active: true)
            : new Box(new BoxStyle { Padding = padding },
                new Text(Label(), BoxType, palette.CellText, maxLines: 1));
        var height = type.LineHeight + padding.Top + padding.Bottom;
        var surface = new CodeSurface(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fixed(height),
        }, text), box)
        {
            OnChanged = () => SetState(() => { }),
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
        }, surface);
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
