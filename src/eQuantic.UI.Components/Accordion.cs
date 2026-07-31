using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>One <see cref="Accordion"/> section: a title row and the content it expands to.</summary>
public sealed record AccordionItem(string Title)
{
    public VisualNode? Content { get; init; }
}

/// <summary>
/// The design system's Accordion (wave 3b): stacked sections whose 48dp header rows toggle their
/// content — the chevron flips, open sections show on the line-height grid, hairline dividers
/// between sections (the List convention). Open state is INTERNAL (like Menu): single-open by
/// default, <see cref="Multiple"/> keeps independent sections.
/// v1 fences: height animation (expand rides the state-transition system later), controlled mode,
/// disabled sections.
/// </summary>
public sealed class Accordion : StatefulComponent
{
    private int _openSingle = -1;
    private readonly HashSet<int> _openMulti = new();

    public Accordion(IReadOnlyList<AccordionItem> items, int openIndex = -1)
    {
        Items = items;
        _openSingle = openIndex;
    }

    public IReadOnlyList<AccordionItem> Items { get; private set; }
    public bool Multiple { get; init; }

    public override void AdoptConfig(UiComponent next)
    {
        if (next is Accordion fresh) Items = fresh.Items;
    }

    private bool IsOpen(int index) => Multiple ? _openMulti.Contains(index) : _openSingle == index;

    private void Toggle(int index)
    {
        SetState(() =>
        {
            if (Multiple)
            {
                if (!_openMulti.Add(index)) _openMulti.Remove(index);
            }
            else
            {
                _openSingle = _openSingle == index ? -1 : index;
            }
        });
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var column = new Column(gap: 0) { Width = SizeValue.Fill };

        for (var i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            var index = i;
            var open = IsOpen(i);

            var header = new Row(gap: Space.S2) { Cross = CrossAlign.Center, Width = SizeValue.Fill };
            header.Add(new Text(item.Title, TypeRole.Label));
            header.Add(new Flexible(new Spacer()));
            header.Add(new Icon(open ? Icons.ChevronUp : Icons.ChevronDown, IconSize.Sm, theme.TextSecondary));

            column.Add(new Pressable(new Box(new BoxStyle
            {
                Height = 48,
                Width = SizeValue.Fill,
                Padding = EdgeInsets.Symmetric(Space.S3, 0),
                Hover = new StyleDiff { Background = theme.SurfaceSubtle },
            }, header), () => Toggle(index)));

            if (open && item.Content is { } content)
            {
                column.Add(new Box(new BoxStyle
                {
                    Width = SizeValue.Fill,
                    Padding = new EdgeInsets(Space.S3, 0, Space.S3, Space.S3),
                }, content));
            }

            if (i < Items.Count - 1) column.Add(new Divider());
        }

        return column;
    }
}
