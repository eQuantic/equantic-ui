using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's NavigationRail (spec B4, the wide half): the SAME destinations a
/// <see cref="BottomNavigation"/> shows on a phone, stood on their side for a tablet or a desktop
/// window. It takes the very same <see cref="NavItem"/> list, so a shell hands one collection to an
/// <c>AdaptiveNode</c> and the bar becomes a rail when the window gets wide — which is the whole
/// point of having both.
/// <para>
/// 80dp wide, destinations from the top, each one a 56×26 pill + label exactly as the bar draws it:
/// a destination that changed shape between the two would read as a different app after a rotation.
/// The optional <see cref="Leading"/> slot is the rail's header, a FAB or a menu button by
/// convention, and it sits above the destinations rather than among them.
/// </para>
/// <para>
/// The destinations sit where <see cref="Alignment"/> says, Material's TOP by default. It is the
/// vocabulary's own <see cref="MainAlign"/> rather than a rail-shaped enum of its own: the rail
/// forwards it straight into the column that lays the destinations out, which is exactly the shape
/// that needed a component's enum property to cross to the twin as a UNION instead of as a bare
/// string — the fence this replaces, and the work that lifted it.
/// </para>
/// <para>
/// v1 fences: the trailing shadow joins the engine shadow primitive, and the start safe-area inset
/// (a landscape phone's notch sits exactly here) joins the host insets, both shared with the bar.
/// </para>
/// </summary>
public sealed class NavigationRail : StatelessComponent
{
    public NavigationRail(IReadOnlyList<NavItem> items, int selected, Action<int>? onSelect = null)
    {
        Items = items;
        Selected = selected;
        OnSelect = onSelect;
    }

    /// <summary>
    /// 3–7 destinations: a rail is taller than a bar is wide, so it carries the two the bar refuses,
    /// and past seven the answer is still a Drawer. Checked HERE rather than inside Build, like the
    /// bar and the AppBar: a contract enforced mid-render throws once per frame, far from the line
    /// that got it wrong, and the boundary that keeps one component's failure off the rest of the
    /// screen would (correctly) contain it into a red panel instead of naming the mistake.
    /// </summary>
    public IReadOnlyList<NavItem> Items
    {
        get;
        init => field = value.Count is < 3 or > 7
            ? throw new ArgumentException("NavigationRail takes 3-7 destinations (spec B4): 2 → Tabs, 8+ → Drawer.", nameof(Items))
            : value;
    }

    public int Selected { get; init; }
    public Action<int>? OnSelect { get; init; }

    /// <summary>The rail's header — a FAB or a menu IconButton by convention. It sits ABOVE the
    /// destinations rather than among them, whatever <see cref="Alignment"/> says.</summary>
    public VisualNode? Leading { get; init; }

    /// <summary>
    /// Where the destinations sit along the rail. <see cref="MainAlign.Start"/> (the top) is
    /// Material's default and what a rail without a header wants; <see cref="MainAlign.Center"/> is
    /// what a rail WITH one usually wants, so the header reads as a header and not as a fourth
    /// destination.
    /// </summary>
    public MainAlign Alignment { get; init; } = MainAlign.Start;

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var primary = theme.Colors(Variant.Primary);

        // C16's own gap, not the bar's: a rail stacks its cells 8 apart.
        var destinations = new Column(gap: Space.S2)
        {
            Width = SizeValue.Fill,
            Cross = CrossAlign.Center,
        };
        for (var i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            var isActive = i == Selected;
            var index = i;

            var glyph = isActive && item.SelectedIcon is { } filled ? filled : item.Icon;
            var tint = isActive ? primary.OnSubtle : theme.TextMuted;

            // C16's figures, which are NOT the bar's: a rail's glyph is Dense 20 in a 52×30 pill
            // under a Caption 12 label, where the bar draws Md 24 in 56×26 under an 11/700 one.
            // Built from the bar, the rail was the bar stood on its side — the same class of
            // mistake as reading a sibling's spec for a component that has its own.
            var icon = new Icon(glyph, IconSize.Dense, tint);
            var iconNode = item.BadgeCount > 0 ? Badge.Over(icon, item.BadgeCount) : (VisualNode)icon;

            var pill = new Box(new BoxStyle
            {
                Width = 52,
                Height = 30,
                Background = isActive ? primary.Subtle : null,
                CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
            }, iconNode.Centered());

            // The cell is 80×56 and the HIT is the whole cell, so the pressable wraps the cell and
            // not just the pill and its label.
            var column = new Column(gap: 2)
            {
                Width = SizeValue.Fill,
                Height = 56,
                Main = MainAlign.Center,
                Cross = CrossAlign.Center,
            };
            column.Add(pill);
            column.Add(new Text(item.Label, TypeRole.Caption,
                tint, maxLines: 1)
            {
                // Selection is also a WEIGHT here (C16: label 700 when selected), not only a tint.
                StyleOverride = isActive ? new TypeStyle(12, 16, FontWeight.Bold, 0, 1.3f) : null,
            });

            destinations.Add(new Pressable(column, OnSelect is null ? null : () => OnSelect(index))
            {
                Label = item.Label,
                PressedBackground = theme.SurfaceSubtle,
                // The bar's twin in this too: a destination states WHERE YOU ARE, and a rail that
                // said it only in a tint would be the same bar with a different silence.
                Role = PressableRole.Destination,
                Selected = isActive,
            });
        }

        // The ALIGNMENT belongs to the outer column: it is the one with the rail's full height, so it
        // is the only one with slack to distribute. Setting it on the destinations (which hug their
        // content) would centre nothing at all.
        var rail = new Column(gap: Space.S2)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Cross = CrossAlign.Center,
            Main = Alignment,
        };
        if (Leading is { } leading) rail.Add(leading);
        rail.Add(destinations);

        return new Box(new BoxStyle
        {
            Width = 80,
            Height = SizeValue.Fill,
            Padding = EdgeInsets.Symmetric(0, Space.S3),
            Background = theme.Surface,
            // C16: a 1dp Border on the TRAILING edge. It is what separates the rail from the content
            // beside it — without it, a rail on a Surface page has no edge at all, and every shell
            // that wanted one drew its own (the sample and both template shells did exactly that).
            BorderWidth = 1,
            BorderColor = theme.Border,
            BorderSides = BorderSides.End,
        }, rail);
    }
}
