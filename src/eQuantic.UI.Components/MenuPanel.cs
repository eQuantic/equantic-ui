using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// What a menu IS, whoever opens it (#1531): the panel of item rows, the highlight the keyboard moves over the
/// enabled ones, and the keys that move and run it. The <see cref="Menu"/>, opened by its trigger, and the
/// <see cref="ContextMenu"/>, opened by a request for the menu of what it wraps, keep their own state and draw
/// the same menu from here.
/// </summary>
internal static class MenuPanel
{
    /// <summary>The panel: one row per item, the highlighted one filled while the menu is open, a disabled one
    /// dimmed and still announced, each choosing through <paramref name="choose"/>.</summary>
    public static VisualNode Build(ComponentContext context, IReadOnlyList<MenuItem> items, bool open, int highlight,
        Action<int> choose)
    {
        var theme = context.Theme;
        // The menus' slot (docs/ELEMENT-TREE-PLAN.md §3.10): each value the theme names, or today's.
        var slot = theme.Menu;

        var list = new Column(gap: 0) { Width = SizeValue.Fill };
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var index = i;

            var row = new Row(gap: slot?.ItemGap ?? Space.S2)
            {
                Cross = CrossAlign.Center,
                Width = SizeValue.Fill,
                Height = SizeValue.Fill,
            };
            if (item.Icon is { } glyph)
                row.Add(new Icon(glyph, IconSize.Dense,
                    item.Destructive ? theme.Colors(Variant.Destructive).Base : theme.TextSecondary));
            // ONE line: a menu item is a single line on every platform that has menus, and a label
            // that does not fit ellipsizes rather than growing the row it lives in — a wrapped
            // label overflowed a fixed-height row and read as broken text.
            row.Add(new Text(item.Label, TypeRole.BodyM,
                item.Destructive ? theme.Colors(Variant.Destructive).Base : theme.TextPrimary,
                maxLines: 1));

            var surface = new Box(new BoxStyle
            {
                Height = ControlMetrics.Height(context, SizeVariant.Medium),
                Padding = slot?.ItemPadding ?? EdgeInsets.Symmetric(Space.S3, 0),
                Width = SizeValue.Fill,
                Opacity = item.Disabled ? theme.DisabledOpacity : null,
                Background = open && index == highlight && !item.Disabled ? theme.SurfaceSubtle : null,
                Hover = item.Disabled ? null : new StyleDiff { Background = theme.SurfaceSubtle },
            }, row);

            // A disabled item is still a MENU ITEM — perceivable and announced dimmed, exactly
            // like every disabled row in a native menu. A bare Box would make it invisible to
            // assistive tech, which reads as the menu having fewer commands than the screen shows.
            list.Add(new Pressable(surface, item.Disabled ? null : () => choose(index))
            {
                Disabled = item.Disabled,
                Role = PressableRole.MenuItem,
                // The keyboard stays on the trigger while the menu shows (the arrows walk the items as
                // its aria-activedescendant), and a press on an item leaves it there too. An item that
                // took the focus left with its menu in the pass its press ran, so a dialog the item
                // opened recorded nothing to give the focus back to (#1812).
                CanRequestFocus = false,
            });
        }

        return new Box(new BoxStyle
        {
            MinWidth = slot?.MenuMinWidth ?? 180,
            Background = theme.Surface,
            CornerRadius = new CornerRadii(theme.Shape(slot?.PanelShape ?? ShapeScale.Medium)),
            BorderWidth = slot?.PanelBorderWidth ?? 1,
            BorderColor = theme.Border,
            Elevation = slot?.PanelElevation ?? 2,
            Padding = slot?.MenuPadding ?? EdgeInsets.Symmetric(0, Space.S1),
            Clip = true,
        }, list);
    }

    /// <summary>
    /// The keys around a menu: ArrowDown and ArrowUp move the highlight, Enter runs it, while
    /// <paramref name="live"/>. Their chords stay around the tree whether the menu is up or not, so opening moves
    /// nothing in it and the focus stays where it was (#567).
    /// </summary>
    public static VisualNode WithKeys(VisualNode menu, bool live, Action down, Action up, Action enter)
    {
        menu = new Shortcut(menu, KeyChord.ArrowDown, down) { Enabled = live };
        menu = new Shortcut(menu, KeyChord.ArrowUp, up) { Enabled = live };
        return new Shortcut(menu, KeyChord.Enter, enter) { Enabled = live };
    }

    /// <summary>The first enabled item, where the highlight starts.</summary>
    public static int FirstEnabled(IReadOnlyList<MenuItem> items)
    {
        for (var i = 0; i < items.Count; i++)
            if (!items[i].Disabled) return i;
        return 0;
    }

    /// <summary>The next enabled item from <paramref name="highlight"/> in <paramref name="direction"/>, stopping at
    /// the edge: a menu that wraps turns a held arrow key into a roulette. Arrows SKIP disabled items, since a
    /// highlight on a row Enter refuses to run is a lie about what Enter does.</summary>
    public static int Step(IReadOnlyList<MenuItem> items, int highlight, int direction)
    {
        for (var i = highlight + direction; i >= 0 && i < items.Count; i += direction)
            if (!items[i].Disabled) return i;
        return highlight;
    }
}
