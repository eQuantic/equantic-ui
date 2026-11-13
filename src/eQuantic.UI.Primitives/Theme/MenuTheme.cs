namespace eQuantic.UI.Primitives;

/// <summary>
/// The slot of what opens beside an anchor: the Popover, the Menu and the Tooltip,
/// every member OPTIONAL: a member left null is the component's own default, today's constant. It holds
/// what the component hard-codes that is LOOK; what is behaviour (a threshold, a duration) stays the
/// component's (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10).
/// <para>
/// <em>How does Flutter solve it?</em> <c>PopupMenuThemeData</c>, <c>MenuThemeData</c> and
/// <c>TooltipThemeData</c> on <c>ThemeData</c>.
/// </para>
/// </summary>
public sealed record MenuTheme
{
    /// <summary>The corners of a popover's and a menu's panel (medium).</summary>
    public ShapeScale? PanelShape { get; init; }

    /// <summary>The panel's border (1).</summary>
    public float? PanelBorderWidth { get; init; }

    /// <summary>The panel's elevation (2).</summary>
    public int? PanelElevation { get; init; }

    /// <summary>Inside a popover (12 all round).</summary>
    public EdgeInsets? PopoverPadding { get; init; }

    /// <summary>A menu's narrowest (180).</summary>
    public float? MenuMinWidth { get; init; }

    /// <summary>Inside a menu's panel, around its items (4 above and below).</summary>
    public EdgeInsets? MenuPadding { get; init; }

    /// <summary>Inside a menu item (12 at the sides).</summary>
    public EdgeInsets? ItemPadding { get; init; }

    /// <summary>Between a menu item's icon and its label (8).</summary>
    public float? ItemGap { get; init; }

    /// <summary>A tooltip's corners (small).</summary>
    public ShapeScale? TooltipShape { get; init; }

    /// <summary>Inside a tooltip (8 at the sides, 4 above and below).</summary>
    public EdgeInsets? TooltipPadding { get; init; }

    /// <summary>A tooltip's fill (the theme's <see cref="IAppTheme.TextPrimary"/>).</summary>
    public ColorToken? TooltipFill { get; init; }

    /// <summary>A tooltip's words (the theme's <see cref="IAppTheme.TextInverse"/>).</summary>
    public ColorToken? TooltipText { get; init; }

    /// <summary>
    /// <paramref name="over"/> laid over <paramref name="under"/>, member by member: a member the over
    /// sets wins, and one it leaves null is the under's. Null only when both are, which is what a theme
    /// built with <c>With</c> answers for a slot neither theme sets.
    /// </summary>
    public static MenuTheme? Merge(MenuTheme? under, MenuTheme? over) =>
        under is null ? over : over is null ? under : new MenuTheme
        {
            PanelShape = over.PanelShape ?? under.PanelShape,
            PanelBorderWidth = over.PanelBorderWidth ?? under.PanelBorderWidth,
            PanelElevation = over.PanelElevation ?? under.PanelElevation,
            PopoverPadding = over.PopoverPadding ?? under.PopoverPadding,
            MenuMinWidth = over.MenuMinWidth ?? under.MenuMinWidth,
            MenuPadding = over.MenuPadding ?? under.MenuPadding,
            ItemPadding = over.ItemPadding ?? under.ItemPadding,
            ItemGap = over.ItemGap ?? under.ItemGap,
            TooltipShape = over.TooltipShape ?? under.TooltipShape,
            TooltipPadding = over.TooltipPadding ?? under.TooltipPadding,
            TooltipFill = over.TooltipFill ?? under.TooltipFill,
            TooltipText = over.TooltipText ?? under.TooltipText,
        };
}
