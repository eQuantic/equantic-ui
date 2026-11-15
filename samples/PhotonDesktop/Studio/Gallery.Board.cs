using eQuantic.UI.Components;
using eQuantic.UI.Primitives;

namespace eQuantic.Studio;

/// <summary>
/// The board (#1537): cards a pointer or the keyboard moves inside a column and across columns, the same
/// board the web dashboard's <c>/board</c> draws, from the same two nodes. Each column is a <c>Sortable</c> of
/// the group <c>board</c> and each card a <c>SortableItem</c>; the board draws where a card would land from
/// the column's hover report and moves the card in its own list when it is dropped. A press on a card that
/// does not travel still opens it.
/// </summary>
public static partial class Gallery
{
    private static readonly string[] BoardColumns = ["To do", "Doing", "Done"];

    private static VisualNode Board(IAppTheme theme, SectionState state, Action<Action> mutate)
    {
        var columns = new Row(gap: Space.S4) { Cross = CrossAlign.Start, Width = SizeValue.Fill };
        foreach (var name in BoardColumns) columns.Add(new Flexible(BoardColumn(theme, state, mutate, name), 1));

        var column = Column(Space.S3);
        column.Add(Caption(theme,
            "Drag a card inside its column or to another one, or focus it and press Space: the arrows carry it, "
            + "Space drops it and Escape puts it back. A press that does not move opens it."));
        column.Add(columns);
        column.Add(Label(theme, state.BoardOpened is null ? "No card opened." : $"Opened: {state.BoardOpened}"));
        return Section(theme, "Board", column);
    }

    private static VisualNode BoardColumn(IAppTheme theme, SectionState state, Action<Action> mutate, string name)
    {
        var cards = state.BoardCards.Where(card => card.Column == name).ToList();
        var list = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        // Where the card would land, drawn by the board from the column's report. The report counts the
        // column without the carried card, which this column still draws, so a place at or after it is one
        // further down here.
        var line = -1;
        if (state.BoardLanding is { } landing && landing.To == name)
        {
            var carried = cards.FindIndex(card => card.Id == landing.Item);
            line = carried >= 0 && carried <= landing.Index ? landing.Index + 1 : landing.Index;
        }
        for (var i = 0; i <= cards.Count; i++)
        {
            if (i == line)
                list.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 3, Background = theme.FocusRing }));
            if (i < cards.Count) list.Add(SortableItem(BoardCardOf(theme, state, mutate, cards[i]), cards[i].Id));
        }

        var body = new Column(gap: Space.S3) { Width = SizeValue.Fill };
        body.Add(new Text($"{name} · {cards.Count}", TypeRole.Label, theme.TextSecondary, maxLines: 1));
        // The whole body of the column is the list, so a card lands in an empty column too.
        body.Add(Sortable(new Box(new BoxStyle { Width = SizeValue.Fill, MinHeight = 160 }, list), name,
            drop => mutate(() => DropCard(state, drop)), group: "board",
            onHovered: drop => mutate(() => state.BoardLanding = drop), label: name));
        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S3),
            Background = theme.SurfaceSubtle,
            CornerRadius = new CornerRadii(Radius.Md),
        }, body);
    }

    private static VisualNode BoardCardOf(IAppTheme theme, SectionState state, Action<Action> mutate, BoardCard card) =>
        new Pressable(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S3),
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
            CornerRadius = new CornerRadii(Radius.Sm),
        }, new Text(card.Title, TypeRole.BodyM, theme.TextPrimary, maxLines: 1)),
        () => mutate(() => state.BoardOpened = card.Title));

    /// <summary>Takes the card out of its column and inserts it where the drop says, counted without it.</summary>
    private static void DropCard(SectionState state, SortDrop drop)
    {
        state.BoardLanding = null;
        var moved = state.BoardCards.First(card => card.Id == drop.Item) with { Column = drop.To };
        var rest = state.BoardCards.Where(card => card.Id != drop.Item).ToList();
        var inColumn = rest.Where(card => card.Column == drop.To).ToList();
        var at = drop.Index < inColumn.Count ? rest.IndexOf(inColumn[drop.Index]) : LastOf(rest, drop.To) + 1;
        rest.Insert(at, moved);
        state.BoardCards = rest;
    }

    private static int LastOf(List<BoardCard> cards, string column)
    {
        var last = -1;
        for (var i = 0; i < cards.Count; i++)
            if (cards[i].Column == column) last = i;
        return last < 0 ? cards.Count - 1 : last;
    }
}
