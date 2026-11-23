namespace eQuantic.UI.Primitives;

/// <summary>
/// The look of an item carried in a <see cref="Sortable"/> list (#1602), every member OPTIONAL: a member left null is
/// <see cref="SortPlacement"/>'s default. A theme sets it for every list (<see cref="IAppTheme.Sortable"/>), and a list
/// lays its own over the theme's (<see cref="Sortable.Style"/>). What is behaviour (how long a stopped copy keeps
/// leaning, how far it leans for a speed) stays the arithmetic's.
/// <para>
/// <em>How does Flutter solve it?</em> <c>ReorderableListView.proxyDecorator</c> wraps the dragged item in the app's
/// look, and its default lifts it on a <c>Material</c> at the theme's elevation. Here the look is the record the rest
/// of the catalog uses, so a theme sets it once, and the shadow is a level of the theme's own scale.
/// </para>
/// </summary>
public sealed record SortableTheme
{
    /// <summary>The opacity of an item in its place while it is carried (0.4), so the place it left still shows.</summary>
    public float? LiftedOpacity { get; init; }

    /// <summary>The opacity of the copy that follows a pointer (0.92).</summary>
    public float? CopyOpacity { get; init; }

    /// <summary>The copy's shadow, a level of the theme's elevation scale (<see cref="IAppTheme.Elevation"/>, 4).</summary>
    public int? CopyElevation { get; init; }

    /// <summary>The most the copy leans with the pointer's speed across, either way, in degrees (12); 0 keeps it
    /// upright, as asking for less motion does.</summary>
    public float? MaxLean { get; init; }

    /// <summary>
    /// <paramref name="over"/> laid over <paramref name="under"/>, member by member: a member the over
    /// sets wins, and one it leaves null is the under's. Null only when both are, which is what a theme
    /// built with <c>With</c> answers for a slot neither theme sets.
    /// </summary>
    public static SortableTheme? Merge(SortableTheme? under, SortableTheme? over) =>
        under is null ? over : over is null ? under : new SortableTheme
        {
            LiftedOpacity = over.LiftedOpacity ?? under.LiftedOpacity,
            CopyOpacity = over.CopyOpacity ?? under.CopyOpacity,
            CopyElevation = over.CopyElevation ?? under.CopyElevation,
            MaxLean = over.MaxLean ?? under.MaxLean,
        };
}
