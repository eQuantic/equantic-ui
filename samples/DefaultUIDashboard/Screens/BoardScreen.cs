using eQuantic.UI.Components;
using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// A board whose cards a pointer moves inside a column and across columns (#1535): each column is a
/// <c>Sortable</c> of the group <c>board</c>, each card a <c>SortableItem</c>. The board draws where a card
/// would land from the column's hover report, and moves the card in its own list when it is dropped. A
/// press on a card that does not travel still opens it.
/// </summary>
[Page("/board", Title = "Board — eQuantic Console")]
public sealed class BoardScreen : StatefulComponent
{
    private static readonly string[] Columns = ["To do", "Doing", "Done"];

    private List<BoardCard> _cards =
    [
        new("c1", "Draw the tree", "To do"),
        new("c2", "Sort the lists", "To do"),
        new("c3", "Read the board aloud", "To do"),
        new("c4", "Ship the preview", "Doing"),
        new("c5", "Write the wiki", "Done"),
    ];

    private SortDrop? _landing;
    private string? _opened;

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var columns = new Row(gap: Space.S4) { Cross = CrossAlign.Start, Width = SizeValue.Fill };
        foreach (var column in Columns) columns.Add(new Flexible(ColumnOf(context, column)));

        return Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
            Padding = EdgeInsets.All(Space.S6),
        },
        Column(gap: Space.S4, children: [
            Text("Board", TypeRole.Heading, theme.TextPrimary),
            Text("Drag a card inside its column or to another one. A press that does not move opens it.",
                TypeRole.BodyM, theme.TextSecondary),
            columns,
            Text(_opened is null ? "No card opened." : $"Opened: {_opened}", TypeRole.BodyL, theme.TextSecondary),
        ]));
    }

    private VisualNode ColumnOf(ComponentContext context, string column)
    {
        var theme = context.Theme;
        var cards = _cards.Where(card => card.Column == column).ToList();
        var list = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        // Where the card would land, drawn by the board from the column's report. The report counts the
        // column without the carried card, which this column still draws, so a place at or after it is one
        // further down here.
        var line = -1;
        if (_landing is { } landing && landing.To == column)
        {
            var carried = cards.FindIndex(card => card.Id == landing.Item);
            line = carried >= 0 && carried <= landing.Index ? landing.Index + 1 : landing.Index;
        }
        for (var i = 0; i <= cards.Count; i++)
        {
            if (i == line)
                list.Add(Box(new BoxStyle { Width = SizeValue.Fill, Height = 3, Background = theme.FocusRing }));
            if (i < cards.Count) list.Add(SortableItem(Card(context, cards[i]), cards[i].Id));
        }

        return Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S3),
            Background = theme.SurfaceSubtle,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
        },
        Column(gap: Space.S3, children: [
            Text($"{column} · {cards.Count}", TypeRole.Label, theme.TextSecondary),
            // The whole body of the column is the list, so a card lands in an empty column too.
            Sortable(Box(new BoxStyle { Width = SizeValue.Fill, MinHeight = 200 }, list), column, Drop,
                group: "board", onHovered: drop => SetState(() => _landing = drop)),
        ]));
    }

    private VisualNode Card(ComponentContext context, BoardCard card)
    {
        var theme = context.Theme;
        return new Pressable(Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S3),
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Small)),
        }, Text(card.Title, TypeRole.BodyM, theme.TextPrimary)), () => SetState(() => _opened = card.Title));
    }

    /// <summary>Takes the card out of its column and inserts it where the drop says, counted without it.</summary>
    private void Drop(SortDrop drop) => SetState(() =>
    {
        _landing = null;
        var moved = _cards.First(card => card.Id == drop.Item) with { Column = drop.To };
        var rest = _cards.Where(card => card.Id != drop.Item).ToList();
        var inColumn = rest.Where(card => card.Column == drop.To).ToList();
        var at = drop.Index < inColumn.Count ? rest.IndexOf(inColumn[drop.Index]) : LastOf(rest, drop.To) + 1;
        rest.Insert(at, moved);
        _cards = rest;
    });

    private static int LastOf(List<BoardCard> cards, string column)
    {
        var last = -1;
        for (var i = 0; i < cards.Count; i++)
            if (cards[i].Column == column) last = i;
        return last < 0 ? cards.Count - 1 : last;
    }
}
