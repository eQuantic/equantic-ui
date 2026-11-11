namespace eQuantic.UI.Primitives;

/// <summary>
/// The calendar family's slot (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10): Calendar and DatePicker, every
/// member OPTIONAL. A member left null is the component's own default. The selected day and today take
/// the primary colours from <see cref="IAppTheme.Colors"/>, which stay there. A DatePicker's panel holds a
/// Calendar, which reads this slot too.
/// <para>
/// <em>How does Flutter solve it?</em> <c>DatePickerThemeData</c> on <c>ThemeData</c>: the day's shape,
/// sizes and colours, the header's style and the container's, nullable.
/// </para>
/// </summary>
public sealed record CalendarTheme
{
    /// <summary>The side of a day's cell, whose circle is half of it (44).</summary>
    public float? CellSize { get; init; }

    /// <summary>The height of the row of weekday names (28).</summary>
    public float? WeekdayHeight { get; init; }

    /// <summary>The gap between the month's title and the paging buttons (4).</summary>
    public float? HeaderGap { get; init; }

    /// <summary>The gap between the header and the grid of days (8).</summary>
    public float? Gap { get; init; }

    /// <summary>The month title's role (<see cref="TypeRole.TitleSmall"/>).</summary>
    public TypeRole? TitleRole { get; init; }

    /// <summary>The month title's colour (<see cref="IAppTheme.TextPrimary"/>).</summary>
    public ColorToken? TitleColor { get; init; }

    /// <summary>A weekday name's role (<see cref="TypeRole.Caption"/>).</summary>
    public TypeRole? WeekdayRole { get; init; }

    /// <summary>A weekday name's colour (<see cref="IAppTheme.TextMuted"/>).</summary>
    public ColorToken? WeekdayColor { get; init; }

    /// <summary>A day's role (<see cref="TypeRole.BodyM"/>).</summary>
    public TypeRole? DayRole { get; init; }

    /// <summary>A day's colour when it is neither selected nor today (<see cref="IAppTheme.TextPrimary"/>).</summary>
    public ColorToken? DayColor { get; init; }

    /// <summary>The fill a hovered day takes (<see cref="IAppTheme.SurfaceSubtle"/>).</summary>
    public ColorToken? DayHoverFill { get; init; }

    /// <summary>The width of the ring that marks today (1.5).</summary>
    public float? TodayRingWidth { get; init; }

    // ---- DatePicker -------------------------------------------------------------------------------

    /// <summary>The fill of the panel the calendar opens in (<see cref="IAppTheme.Surface"/>).</summary>
    public ColorToken? PanelFill { get; init; }

    /// <summary>The panel's corner radius (the theme's medium shape).</summary>
    public float? PanelRadius { get; init; }

    /// <summary>The panel's border width (1).</summary>
    public float? PanelBorderWidth { get; init; }

    /// <summary>The panel's border colour (<see cref="IAppTheme.Border"/>).</summary>
    public ColorToken? PanelBorderColor { get; init; }

    /// <summary>The panel's elevation level (2).</summary>
    public int? PanelElevation { get; init; }

    /// <summary>The panel's padding (12).</summary>
    public float? PanelPadding { get; init; }

    /// <summary>The size of the glyph that opens the panel (20), a size on the §07 whitelist (16, 20, 24
    /// or 32): an icon of any other size throws when it is built.</summary>
    public float? OpenerIconSize { get; init; }

    /// <summary>That glyph's colour (<see cref="IAppTheme.TextSecondary"/>).</summary>
    public ColorToken? OpenerColor { get; init; }
}
