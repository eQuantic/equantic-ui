using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's ListItem (spec B2): leading (Icon Md 24 / Avatar 40 / none) · content
/// (title 15/500 + subtitle 13) · trailing (Caption meta / any node — chevron, Switch, Checkbox).
/// Padding X S4, slot gap S3, min heights 52 / 68 / 88 for one, two and three lines. Title
/// truncates before the trailing meta moves — the A2 contract. Pressable when
/// <see cref="OnPressed"/> is set (SurfaceSubtle pressed fill).
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

    /// <summary>Leading slot — Icon Md 24 or Avatar 40 by convention.</summary>
    public VisualNode? Leading { get; init; }

    /// <summary>
    /// How wide the <see cref="Leading"/> slot is (dp), which is what puts the divider below this row
    /// under its TEXT rather than across its icon — the handoff's B2 rule, 16 + leading + gap.
    /// <para>
    /// 24 unless said otherwise, the Icon Md convention. An <c>Avatar</c> is 40, and a slot the app
    /// built itself is whatever it built: the component holds a <see cref="VisualNode"/> and cannot
    /// measure it before layout, so this is the one number it has to be told.
    /// </para>
    /// </summary>
    public float LeadingWidth { get; init; } = 24;

    /// <summary>Trailing slot — Caption meta, chevron Icon, Switch or Checkbox.</summary>
    public VisualNode? Trailing { get; init; }

    /// <summary>
    /// This row is the DESTINATION the user is on — a drawer's nav list, a list-detail's chosen
    /// item. It tints the row and states it as <c>aria-current="page"</c> (the selected trait on the
    /// mobiles), because selection that lives only in a fill colour is invisible to assistive tech.
    /// <para>
    /// Only meaningful with <see cref="OnPressed"/>: an inert row is not somewhere you can be.
    /// </para>
    /// <para>
    /// FENCE — the handoff's B2 draws a line this does not yet cross. A NAVIGATION is a landmark of
    /// destinations stating <c>aria-current="page"</c> (B4), which is what this is; a SELECTABLE list
    /// is a <c>listbox</c> of <c>option</c> rows stating <c>aria-selected</c>, and one tab stop for
    /// the whole thing (↑/↓ move, Space toggles, Home/End jump). The second contract is not offered
    /// here on purpose: the roving stop belongs to the CONTAINER, and until <see cref="List"/>
    /// composes it, an <c>option</c> row would leave the tab order with nothing to put it back —
    /// a knob that breaks the keyboard is worse than a missing one.
    /// </para>
    /// </summary>
    public bool Selected { get; init; }

    /// <summary>
    /// Lines the SUBTITLE may take — 1 (a 68dp row) or 2 (the handoff's 88dp three-line row, for
    /// settings copy that needs a second sentence of context). The title is always one line: it
    /// truncates before the trailing meta moves, which is the A2 contract.
    /// </summary>
    public int SubtitleLines { get; init; } = 1;

    /// <summary>The row's own minimum height — 52 / 68 / 88 for one, two and three lines.</summary>
    internal float MinHeight => Subtitle is null ? 52 : SubtitleLines >= 2 ? 88 : 68;

    /// <summary>Where this row's TEXT starts, and so where the divider under it begins: the row's own
    /// padding, plus the leading slot and the gap after it when there is one.</summary>
    internal float ContentInset => Leading is null ? Space.S4 : Space.S4 + LeadingWidth + Space.S3;

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        var content = new Column(gap: 2);
        content.Add(new Text(Title, TypeRole.BodyM, theme.TextPrimary, maxLines: 1)
        {
            StyleOverride = new TypeStyle(15, 20, FontWeight.Medium, 0, 1.3f),
        });
        if (Subtitle is { } subtitle)
            content.Add(new Text(subtitle, TypeRole.Caption, theme.TextSecondary,
                maxLines: SubtitleLines >= 2 ? 2 : 1));

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
            MinHeight = MinHeight,
            Background = Selected ? theme.Colors(Variant.Primary).Subtle : null,
        }, row);

        return OnPressed is null
            ? body
            : new Pressable(body, Disabled ? null : OnPressed)
            {
                Disabled = Disabled,
                Label = Title,
                PressedBackground = theme.SurfaceSubtle,
                // Stated only when the row IS one: a plain list row that announced "not current"
                // for every entry would be noise on every list in the app.
                Role = Selected ? PressableRole.Destination : PressableRole.Button,
                Selected = Selected ? true : null,
            };
    }
}

/// <summary>
/// The design system's List (spec B2): ListItems separated by leading-inset dividers — the LIST owns
/// its dividers (hand-placed Dividers between items fail review, spec A7), and insets each one to
/// where the row ABOVE starts its text, so a hairline never cuts across an icon.
/// <para>
/// v1 fences: recycling/virtualization joins the List engine work (this renders bounded item sets),
/// and a SELECTABLE list is still a row of individual tab stops rather than B2's one-stop listbox —
/// see <see cref="ListItem.Selected"/> for why that fence is where it is.
/// </para>
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
            // The row ABOVE decides: the hairline continues that row's text edge.
            if (Dividers && i < Items.Count - 1)
                column.Add(new Divider(DividerInset.Leading) { LeadingInset = Items[i].ContentInset });
        }
        return column;
    }
}
