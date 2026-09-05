namespace eQuantic.UI.Primitives;

/// <summary>
/// The colours a chart draws DATA with — as distinct from the colours the theme draws the
/// interface with. A chart never picks a colour; it asks the theme (<see cref="IAppTheme.Data"/>)
/// and every colour it is handed does exactly one of four jobs: <see cref="Series"/> encode
/// IDENTITY (which series), <see cref="Sequential"/> encodes MAGNITUDE (how much),
/// <see cref="Diverging"/> encodes POLARITY (which side of a baseline) and <see cref="Status"/>
/// encodes STATE (good to critical). <see cref="Other"/> is the one gray for what the story is not
/// about.
/// <para>
/// The rules the jobs carry are enforced by construction where they can be: a series takes its slot
/// once (colour follows the entity, never its rank, so a filter never repaints the survivors); the
/// order of the slots is the colour-vision-safety mechanism and is never cycled — past
/// <see cref="SeriesCeiling"/> a chart folds the tail into <see cref="Other"/> or facets, and
/// <see cref="SeriesColor"/> refuses a ninth by name; text never wears a series colour, identity
/// rides the coloured mark beside it.
/// </para>
/// <para>
/// <see cref="Default"/> is the validated reference instance of the data-visualization method the
/// SDK adopts. A brand theme that overrides it holds the result to the same audit
/// (<see cref="PaletteAudit"/>) — which is what makes a palette safe to change.
/// </para>
/// </summary>
public sealed record DataPalette
{
    /// <summary>The categorical ceiling. Eight hues are as many as clear the colour-vision gates on
    /// the adjacent pairlist in both modes; a ninth is indistinguishable from one of them for
    /// somebody, so it is never generated.</summary>
    public const int SeriesCeiling = 8;

    public DataPalette(IReadOnlyList<ColorToken> series, IReadOnlyList<ColorToken> sequential,
        DivergingScale diverging, ColorToken other, StatusScale status)
    {
        if (series.Count != SeriesCeiling)
            throw new ArgumentException(
                $"A data palette carries exactly {SeriesCeiling} series slots in a fixed order (it has {series.Count}). " +
                "The order is the colour-vision-safety mechanism; fewer slots is a palette that cannot serve a chart with that many series, more is a ninth hue nobody can tell apart.",
                nameof(series));
        if (sequential.Count < 2)
            throw new ArgumentException("A sequential ramp needs at least two steps.", nameof(sequential));
        Series = series;
        Sequential = sequential;
        Diverging = diverging;
        Other = other;
        Status = status;
    }

    /// <summary>Identity: eight hues in a FIXED order, assigned to series in sequence.</summary>
    public IReadOnlyList<ColorToken> Series { get; }

    /// <summary>Magnitude: one hue, light to dark. In dark mode the anchor flips — the step that means
    /// "near zero" recedes toward the (dark) surface — so a token's dark half is the ramp read from
    /// the other end.</summary>
    public IReadOnlyList<ColorToken> Sequential { get; }

    /// <summary>Polarity: two poles that read as opposite, and a midpoint that reads as nothing.</summary>
    public DivergingScale Diverging { get; }

    /// <summary>De-emphasis — the gray of "the rest" and of "Other": the context a highlighted series
    /// is read against.</summary>
    public ColorToken Other { get; }

    /// <summary>State — reserved, never themed into a series slot, and never carried by colour alone.</summary>
    public StatusScale Status { get; }

    /// <summary>
    /// The colour of the series at <paramref name="index"/> (0-based), or an exception by name past
    /// the ceiling — never a generated hue.
    /// </summary>
    public ColorToken SeriesColor(int index)
    {
        if (index < 0 || index >= Series.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index,
                $"A chart carries at most {SeriesCeiling} series colours. Fold the tail into Other, facet into small multiples, " +
                "or encode a second channel (shape) — a ninth hue is indistinguishable from an existing one under colour-vision deficiency.");
        return Series[index];
    }

    /// <summary>
    /// The validated reference instance: eight hues that clear every colour-vision gate in both modes
    /// on the adjacent pairlist (and whose first three clear it on all pairs, the cap for scatter and
    /// small multiples), a blue sequential ramp, a blue–red diverging pair on a neutral gray, and the
    /// four fixed status steps. The numbers the method publishes for it are pinned by test.
    /// </summary>
    public static DataPalette Default { get; } = new(
        series:
        [
            Tok(0x2a78d6, 0x3987e5), // 1 blue
            Tok(0xeb6834, 0xd95926), // 2 orange
            Tok(0x1baf7a, 0x199e70), // 3 aqua
            Tok(0xeda100, 0xc98500), // 4 yellow
            Tok(0xe87ba4, 0xd55181), // 5 magenta
            Tok(0x008300, 0x008300), // 6 green
            Tok(0x4a3aa7, 0x9085e9), // 7 violet
            Tok(0xe34948, 0xe66767), // 8 red
        ],
        sequential: Ramp(0xcde2fb, 0x9ec5f4, 0x6da7ec, 0x3987e5, 0x256abf, 0x184f95, 0x0d366b),
        diverging: new DivergingScale(
            Negative: Tok(0x2a78d6, 0x3987e5),
            Midpoint: Tok(0xf0efec, 0x383835),
            Positive: Tok(0xe34948, 0xe66767)),
        other: Tok(0x898781, 0x898781),
        status: new StatusScale(
            Good: Tok(0x0ca30c, 0x0ca30c),
            Warning: Tok(0xfab219, 0xfab219),
            Serious: Tok(0xec835a, 0xec835a),
            Critical: Tok(0xd03b3b, 0xd03b3b)));

    private static ColorToken Tok(uint light, uint dark) => new(Rgb(light), Rgb(dark));

    private static Color Rgb(uint hex) => Color.FromRgb((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex);

    /// <summary>A one-hue ramp given light to dark; the dark half of each token is the same ramp read
    /// from the other end, which is the anchor flip.</summary>
    private static ColorToken[] Ramp(params uint[] steps)
    {
        var tokens = new ColorToken[steps.Length];
        for (var i = 0; i < steps.Length; i++)
            tokens[i] = new ColorToken(Rgb(steps[i]), Rgb(steps[steps.Length - 1 - i]));
        return tokens;
    }
}

/// <summary>Two poles that read as opposite — warm against cool — and a midpoint that reads as
/// nothing (a neutral gray, never a hue). Steps between a pole and the midpoint are interpolated by
/// the chart, equally on both arms.</summary>
public readonly record struct DivergingScale(ColorToken Negative, ColorToken Midpoint, ColorToken Positive);

/// <summary>The four reserved state steps. Deliberately distinct from the series slots so a status
/// colour never impersonates a series, and always shown with an icon and a label — on a light
/// surface, warning and serious sit below 3:1 by design and the pairing is the mitigation.</summary>
public readonly record struct StatusScale(ColorToken Good, ColorToken Warning, ColorToken Serious, ColorToken Critical);
