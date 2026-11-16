using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

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

    /// <summary>
    /// The 3-5 destinations of spec B4, checked HERE rather than inside Build: a contract enforced
    /// mid-render throws once per frame, far from the line that got it wrong, and the boundary that
    /// keeps one component's failure off the rest of the page would (correctly) contain it into a
    /// red panel instead of naming the mistake. Dialog and TextInput already validate this way.
    /// </summary>
    public IReadOnlyList<NavItem> Items
    {
        get;
        init => field = value.Count is < 3 or > 5
            ? throw new ArgumentException("BottomNavigation takes 3-5 destinations (spec B4): 2 → Tabs, 6+ → Drawer.", nameof(Items))
            : value;
    }
    public int Selected { get; init; }
    public Action<int>? OnSelect { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var primary = theme.Colors(Variant.Primary);
        // The theme's Navigation slot, then the component's own (§3.10).
        var slot = theme.Navigation;
        var row = new Row(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        for (var i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            var isActive = i == Selected;
            var index = i;

            var glyph = isActive && item.SelectedIcon is { } filled ? filled : item.Icon;
            var tint = isActive ? primary.OnSubtle : slot?.InactiveColor ?? theme.TextMuted;

            var icon = new Icon(glyph, slot?.BarIconSize ?? IconSize.Md, tint);
            var iconNode = item.BadgeCount > 0 ? Badge.Over(icon, item.BadgeCount) : (VisualNode)icon;

            var pillContent = iconNode.Centered();
            var pill = new Box(new BoxStyle
            {
                Width = slot?.BarPillWidth ?? 56,
                Height = slot?.BarPillHeight ?? 26,
                Background = isActive ? primary.Subtle : null,
                CornerRadius = new CornerRadii(slot?.PillRadius ?? theme.Shape(ShapeScale.Full)),
                // B4: "Press-down shows the pill at 40%". The press previews the selection's own pill,
                // never SurfaceSubtle, which B4 gives the hover, and stays on the pill, never on the
                // whole column. The selected destination's pill already shows, and a press never
                // trades it for another fill: that would read as the selection going away.
                Pressed = isActive ? null : new StyleDiff { Background = primary.Subtle.WithOpacity(slot?.PillPressOpacity ?? 0.4f) },
            }, pillContent);

            var column = new Column(gap: slot?.PillLabelGap ?? 2)
            {
                Height = SizeValue.Fill,
                Main = MainAlign.Center,
                Cross = CrossAlign.Center,
            };
            column.Add(pill);
            column.Add(new Text(item.Label, TypeRole.Caption, tint, maxLines: 1)
            {
                StyleOverride = new TypeStyle(slot?.BarLabelSize ?? 11, slot?.BarLabelLineHeight ?? 14,
                    slot?.BarLabelWeight ?? FontWeight.Bold, slot?.BarLabelTracking ?? 0.1f, 1.3f),
            });

            row.Add(new Flexible(new Pressable(column, OnSelect is null ? null : () => OnSelect(index))
            {
                Label = item.Label,
                // The pill and the tint say "you are here" to everyone who can see them. This says
                // it to everyone else: aria-current="page" on the web, the selected trait on both
                // mobiles.
                Role = PressableRole.Destination,
                Selected = isActive,
            }));
        }

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = slot?.BarHeight ?? 56,
            Background = slot?.Background ?? theme.Surface,
        }, row);
    }
}
