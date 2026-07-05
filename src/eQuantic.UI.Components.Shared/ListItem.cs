using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components.Shared;

/// <summary>
/// The design system's ListItem (spec B2): leading (Icon Lg / Avatar / none) · content (title
/// 15/500 + subtitle 13, one line each) · trailing (Caption meta / any node — chevron, Switch,
/// Checkbox). Padding X S4, slot gap S3, min heights 52/68 (1-/2-line; the 3-line 88 variant joins
/// with 2-line subtitles). Title truncates before the trailing meta moves — the A2 contract.
/// Pressable when <see cref="OnPressed"/> is set (SurfaceSubtle pressed fill).
/// </summary>
public sealed class ListItem : StatelessComponent
{
    public ListItem(string title, string? subtitle = null, Action? onPressed = null)
    {
        Title = title;
        Subtitle = subtitle;
        OnPressed = onPressed;
    }

    public string Title { get; init; }
    public string? Subtitle { get; init; }
    public Action? OnPressed { get; init; }
    public bool Disabled { get; init; }

    /// <summary>Leading slot — Icon Lg 24 or Avatar 40 by convention.</summary>
    public VisualNode? Leading { get; init; }

    /// <summary>Trailing slot — Caption meta, chevron Icon, Switch or Checkbox.</summary>
    public VisualNode? Trailing { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        var content = new Column(gap: 2);
        content.Add(new Text(Title, TypeRole.BodyM, theme.TextPrimary, maxLines: 1)
        {
            StyleOverride = new TypeStyle(15, 20, FontWeight.Medium, 0, 1.3f),
        });
        if (Subtitle is { } subtitle)
            content.Add(new Text(subtitle, TypeRole.Caption, theme.TextSecondary, maxLines: 1));

        var row = new Row(gap: Space.S3)
        {
            Width = SizeValue.Fill,
            Cross = CrossAlign.Center,
            Padding = EdgeInsets.Symmetric(Space.S4, Space.S2),
        };
        if (Leading is { } leading) row.Add(leading);
        row.Add(new Flexible(content));
        if (Trailing is { } trailing) row.Add(trailing);

        var body = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            MinHeight = Subtitle is null ? 52 : 68,
        }, row);

        return OnPressed is null
            ? body
            : new Pressable(body, Disabled ? null : OnPressed)
            {
                Disabled = Disabled,
                Label = Title,
                PressedBackground = theme.SurfaceSubtle,
            };
    }
}

/// <summary>
/// The design system's List (spec B2): ListItems separated by leading-inset dividers — the LIST owns
/// its dividers (hand-placed Dividers between items fail review, spec A7). v1 fence: recycling/
/// virtualization joins the List engine work; this renders bounded item sets.
/// </summary>
public sealed class List : StatelessComponent
{
    public List(IReadOnlyList<ListItem> items, bool dividers = true)
    {
        Items = items;
        Dividers = dividers;
    }

    public IReadOnlyList<ListItem> Items { get; init; }
    public bool Dividers { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var column = new Column(gap: 0) { Width = SizeValue.Fill };
        for (var i = 0; i < Items.Count; i++)
        {
            column.Add(Items[i]);
            if (Dividers && i < Items.Count - 1)
                column.Add(new Divider(DividerInset.Leading));
        }
        return column;
    }
}
