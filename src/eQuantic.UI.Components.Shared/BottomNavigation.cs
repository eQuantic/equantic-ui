using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components.Shared;

/// <summary>One BottomNavigation destination (spec B4): glyph pair (outline/filled) + a REQUIRED
/// label; <see cref="BadgeCount"/> anchors a count Badge (B7) to the icon's top-end corner.</summary>
public sealed record NavItem(Icons Icon, string Label, Icons? SelectedIcon = null)
{
    public int BadgeCount { get; init; }
}

/// <summary>
/// The design system's BottomNavigation (spec B4), CONTROLLED: 3–5 destinations (2 → use Tabs;
/// 6+ → Drawer; out of range throws), equal-width columns where the WHOLE column is the target.
/// Item = icon in a 56×26 pill slot + 11/700 label — labels are ALWAYS visible (icon-only nav
/// fails review). Active: Primary-subtle pill + filled glyph + OnSubtle label; inactive: TextMuted
/// outline glyph. Badges anchor to the icon's top-end. v1 fences: the E2 top shadow joins the
/// engine shadow primitive; the bottom safe-area inset joins the host insets.
/// </summary>
public sealed class BottomNavigation : StatelessComponent
{
    public BottomNavigation(IReadOnlyList<NavItem> items, int selected, Action<int>? onSelect = null)
    {
        Items = items;
        Selected = selected;
        OnSelect = onSelect;
    }

    public IReadOnlyList<NavItem> Items { get; init; }
    public int Selected { get; init; }
    public Action<int>? OnSelect { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var primary = theme.Colors(Variant.Primary);
        if (Items.Count is < 3 or > 5)
            throw new ArgumentException("BottomNavigation takes 3-5 destinations (spec B4): 2 → Tabs, 6+ → Drawer.");

        var row = new Row(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        for (var i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            var isActive = i == Selected;
            var index = i;

            var glyph = isActive && item.SelectedIcon is { } filled ? filled : item.Icon;
            var tint = isActive ? primary.OnSubtle : theme.TextMuted;

            VisualNode iconNode = new Icon(glyph, IconSize.Md, tint);
            if (item.BadgeCount > 0)
            {
                var stack = new Stack();
                stack.Add(iconNode);
                stack.Add(new Positioned(new Badge(item.BadgeCount) { Ring = true }, top: -4, end: -8));
                iconNode = stack;
            }

            var pillContent = new Row(gap: 0) { Main = MainAlign.Center, Height = SizeValue.Fill };
            pillContent.Add(iconNode);
            var pill = new Box(new BoxStyle
            {
                Width = 56,
                Height = 26,
                Background = isActive ? primary.Subtle : null,
                CornerRadius = new CornerRadii(Radius.Full),
            }, pillContent);

            var column = new Column(gap: 2)
            {
                Height = SizeValue.Fill,
                Main = MainAlign.Center,
                Cross = CrossAlign.Center,
            };
            column.Add(pill);
            column.Add(new Text(item.Label, TypeRole.Caption, tint, maxLines: 1)
            {
                StyleOverride = new TypeStyle(11, 14, FontWeight.Bold, 0.1f, 1.3f),
            });

            row.Add(new Flexible(new Pressable(column, OnSelect is null ? null : () => OnSelect(index))
            {
                Label = item.Label,
                PressedBackground = theme.SurfaceSubtle,
            }));
        }

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = 56,
            Background = theme.Surface,
        }, row);
    }
}
