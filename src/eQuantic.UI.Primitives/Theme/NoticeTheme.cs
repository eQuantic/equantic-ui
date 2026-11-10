namespace eQuantic.UI.Primitives;

/// <summary>
/// The slot of what tells the reader something happened: the Toast, the Banner and the CookieConsent,
/// every member OPTIONAL: a member left null is the component's own default, today's constant. It holds
/// what the component hard-codes that is LOOK; what is behaviour (a threshold, a duration) stays the
/// component's (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10).
/// <para>
/// <em>How does Flutter solve it?</em> <c>SnackBarThemeData</c> and <c>MaterialBannerThemeData</c> on
/// <c>ThemeData</c>.
/// </para>
/// </summary>
public sealed record NoticeTheme
{
    /// <summary>A toast's fill (the theme's <see cref="IAppTheme.TextPrimary"/>).</summary>
    public ColorToken? ToastFill { get; init; }

    /// <summary>A toast's words (the theme's <see cref="IAppTheme.TextInverse"/>).</summary>
    public ColorToken? ToastText { get; init; }

    /// <summary>A toast's corners (full).</summary>
    public ShapeScale? ToastShape { get; init; }

    /// <summary>A toast's elevation (3).</summary>
    public int? ToastElevation { get; init; }

    /// <summary>Inside a toast (16 at the sides, 12 above and below).</summary>
    public EdgeInsets? ToastPadding { get; init; }

    /// <summary>A toast's widest (480).</summary>
    public float? ToastMaxWidth { get; init; }

    /// <summary>The status dot's size (8).</summary>
    public float? ToastDot { get; init; }

    /// <summary>Between a toast's dot, words and action (12).</summary>
    public float? ToastGap { get; init; }

    /// <summary>The room a toast keeps from the screen's edges (16 at the sides, 24 above and below).</summary>
    public EdgeInsets? ToastMargin { get; init; }

    /// <summary>A banner's corners (large).</summary>
    public ShapeScale? BannerShape { get; init; }

    /// <summary>Inside a banner (14 at the sides, 12 above and below).</summary>
    public EdgeInsets? BannerPadding { get; init; }

    /// <summary>Between a banner's icon and its words (10).</summary>
    public float? BannerIconGap { get; init; }

    /// <summary>Between a banner's title and its body (4).</summary>
    public float? BannerGap { get; init; }

    /// <summary>A banner's title (13 over 18, semi-bold).</summary>
    public TypeStyle? BannerTitle { get; init; }

    /// <summary>A banner's body (13 over 18, regular).</summary>
    public TypeStyle? BannerBody { get; init; }

    /// <summary>The cookie consent's corners (large).</summary>
    public ShapeScale? ConsentShape { get; init; }

    /// <summary>The cookie consent's elevation (2).</summary>
    public int? ConsentElevation { get; init; }

    /// <summary>The cookie consent's border (1).</summary>
    public float? ConsentBorderWidth { get; init; }

    /// <summary>Inside the cookie consent (16 all round).</summary>
    public EdgeInsets? ConsentPadding { get; init; }
}
