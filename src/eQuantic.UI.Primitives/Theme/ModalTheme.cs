namespace eQuantic.UI.Primitives;

/// <summary>
/// The slot of the surfaces that rise over a page: the Dialog, the BottomSheet and the Drawer,
/// every member OPTIONAL: a member left null is the component's own default, today's constant. It holds
/// what the component hard-codes that is LOOK; what is behaviour (a threshold, a duration) stays the
/// component's (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10).
/// <para>
/// <em>How does Flutter solve it?</em> <c>DialogThemeData</c>, <c>BottomSheetThemeData</c> and
/// <c>DrawerThemeData</c> on <c>ThemeData</c>: nullable members the widget resolves against its
/// defaults.
/// </para>
/// </summary>
public sealed record ModalTheme
{
    /// <summary>The dialog's widest (480).</summary>
    public float? DialogMaxWidth { get; init; }

    /// <summary>The dialog's corners (extra large).</summary>
    public ShapeScale? DialogShape { get; init; }

    /// <summary>The dialog's elevation (5).</summary>
    public int? DialogElevation { get; init; }

    /// <summary>Inside the dialog (20 all round).</summary>
    public EdgeInsets? DialogPadding { get; init; }

    /// <summary>Between the dialog's words and its actions (20).</summary>
    public float? DialogGap { get; init; }

    /// <summary>Between the dialog's title and its body (8).</summary>
    public float? DialogTextGap { get; init; }

    /// <summary>Between the dialog's actions (8).</summary>
    public float? DialogActionGap { get; init; }

    /// <summary>The least room the dialog leaves at each side of the screen (24).</summary>
    public float? DialogGutter { get; init; }

    /// <summary>The sheet's top corners (extra large).</summary>
    public ShapeScale? BottomSheetShape { get; init; }

    /// <summary>The sheet's elevation (4).</summary>
    public int? BottomSheetElevation { get; init; }

    /// <summary>Inside the sheet (20 at the sides, 12 on top, 24 below).</summary>
    public EdgeInsets? BottomSheetPadding { get; init; }

    /// <summary>Between the sheet's handle and its content (12).</summary>
    public float? BottomSheetGap { get; init; }

    /// <summary>The drag handle's width (32).</summary>
    public float? HandleWidth { get; init; }

    /// <summary>The drag handle's height (4).</summary>
    public float? HandleHeight { get; init; }

    /// <summary>The drag handle's colour (the theme's <see cref="IAppTheme.BorderStrong"/>).</summary>
    public ColorToken? HandleColor { get; init; }

    /// <summary>The drawer's elevation (3).</summary>
    public int? DrawerElevation { get; init; }

    /// <summary>Inside the drawer (16 all round).</summary>
    public EdgeInsets? DrawerPadding { get; init; }

    /// <summary>
    /// <paramref name="over"/> laid over <paramref name="under"/>, member by member: a member the over
    /// sets wins, and one it leaves null is the under's. Null only when both are, which is what a theme
    /// built with <c>With</c> answers for a slot neither theme sets.
    /// </summary>
    public static ModalTheme? Merge(ModalTheme? under, ModalTheme? over) =>
        under is null ? over : over is null ? under : new ModalTheme
        {
            DialogMaxWidth = over.DialogMaxWidth ?? under.DialogMaxWidth,
            DialogShape = over.DialogShape ?? under.DialogShape,
            DialogElevation = over.DialogElevation ?? under.DialogElevation,
            DialogPadding = over.DialogPadding ?? under.DialogPadding,
            DialogGap = over.DialogGap ?? under.DialogGap,
            DialogTextGap = over.DialogTextGap ?? under.DialogTextGap,
            DialogActionGap = over.DialogActionGap ?? under.DialogActionGap,
            DialogGutter = over.DialogGutter ?? under.DialogGutter,
            BottomSheetShape = over.BottomSheetShape ?? under.BottomSheetShape,
            BottomSheetElevation = over.BottomSheetElevation ?? under.BottomSheetElevation,
            BottomSheetPadding = over.BottomSheetPadding ?? under.BottomSheetPadding,
            BottomSheetGap = over.BottomSheetGap ?? under.BottomSheetGap,
            HandleWidth = over.HandleWidth ?? under.HandleWidth,
            HandleHeight = over.HandleHeight ?? under.HandleHeight,
            HandleColor = over.HandleColor ?? under.HandleColor,
            DrawerElevation = over.DrawerElevation ?? under.DrawerElevation,
            DrawerPadding = over.DrawerPadding ?? under.DrawerPadding,
        };
}
