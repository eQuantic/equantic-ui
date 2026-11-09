using eQuantic.UI.Charts;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// The chart layer's first component, drawn by the web realizer from the same code the Photon
/// gallery draws it with (docs/CHARTS-PLAN.md, slice 1): grouped and stacked columns, horizontal
/// bars, a legend that isolates, a tooltip that follows the pointer, and the table view behind the
/// footer. Written with the declarative surface — <c>BarChart(...)</c>, <c>ChartSeries(...)</c> —
/// which is what a consumer's dashboard reads like.
/// </summary>
[Page("/charts", Title = "Charts — eQuantic Console")]
public sealed class ChartsScreen : StatefulComponent
{
    private static readonly string[] Quarters = ["Q1", "Q2", "Q3", "Q4"];

    /// <summary>Whether the compact drawer is up — page state wherever the page is.</summary>
    private bool _navOpen;

    public override VisualNode Build(ComponentContext context) =>
        ConsoleShell.Frame(context.Theme, "/charts", "Charts", Content(context),
            _navOpen, () => SetState(() => _navOpen = !_navOpen));

    // "N0", not "C0": this sample negotiates NEUTRAL cultures ("en", "pt-BR", "es" — see Program.cs),
    // and a neutral culture has no currency, so "C0" would print the generic ¤. A currency format
    // belongs to an app whose cultures are specific ("en-US"); the label still follows the request's
    // culture — 120,000 here, 120.000 under /pt-BR.
    private static VisualNode Content(ComponentContext context)
    {
        var theme = context.Theme;
        var page = new Column(gap: Space.S4, padding: EdgeInsets.All(Space.S4)) { Width = SizeValue.Fill };

        page.Add(new Text("Revenue by quarter", TypeRole.Title, theme.TextPrimary, maxLines: 1));
        page.Add(new Text("One component, three arrangements. Hover a bar, press a legend entry, or switch to the table.",
            TypeRole.BodyM, theme.TextSecondary));
        page.Add(JumpTo(theme));

        var wide = new Row(gap: Space.S4, cross: CrossAlign.Start, wrap: true, runGap: Space.S4);
        wide.Add(new Flexible(Card(theme, Grouped, BarChart(
            title: "Grouped",
            subtitle: "Three regions, side by side",
            series: Regions(),
            categories: CategoryAxis(Quarters),
            values: ValueAxis(Format: "N0", Title: "Revenue")))));
        wide.Add(new Flexible(Card(theme, Stacked, BarChart(
            title: "Stacked",
            subtitle: "The same regions, one on top of another",
            series: Regions(),
            categories: CategoryAxis(Quarters),
            values: ValueAxis(Format: "N0"),
            layout: BarLayout.Stacked))));
        page.Add(wide);

        page.Add(Card(theme, Horizontal, BarChart(
            title: "Horizontal",
            subtitle: "Long category names read best this way",
            series: [ChartSeries("Tickets closed", [42, 31, 57, 12, 26])],
            categories: CategoryAxis(["Billing", "Onboarding", "Performance", "Security", "Accessibility"]),
            values: ValueAxis(Format: "N0"),
            orientation: ChartOrientation.Horizontal,
            plotHeight: 200)));

        return page;
    }

    private static IReadOnlyList<ChartSeries> Regions() =>
    [
        ChartSeries("Europe", [120_000, 138_500, 149_200, 171_000]),
        ChartSeries("Americas", [98_400, 104_900, 99_700, 132_300]),
        ChartSeries("Asia", [61_200, 72_800, 88_100, 95_500]),
    ];

    // One spelling per section: the card claims the name and the "Jump to" row points at it. Two
    // literals drift, and a link that stopped pointing anywhere looks exactly like one that still
    // does. DashboardDocumentTests reads the served page and fails on the first that does.
    private const string Grouped = "grouped";
    private const string Stacked = "stacked";
    private const string Horizontal = "horizontal";

    /// <summary>The places this page's own links point at, in the order they appear.</summary>
    private static readonly (string Fragment, string Label)[] Sections =
    [
        (Grouped, "Grouped"),
        (Stacked, "Stacked"),
        (Horizontal, "Horizontal"),
    ];

    /// <summary>
    /// In-page navigation: a row of links, each landing on the card that claims its name.
    /// <para>
    /// No sample linked to a fragment before this row, which is how the router defect fixed in #100
    /// survived a release: a <see cref="VisualNode.Bookmark"/> is somewhere the page actually GOES,
    /// and nothing here ever asked it to go there. The links are the ordinary <c>Link</c> with a
    /// fragment for a destination. The router leaves a same-page hash to the browser, and the
    /// realizer gives the target room to land under whatever chrome the app pinned.
    /// </para>
    /// </summary>
    private static VisualNode JumpTo(IAppTheme theme)
    {
        var row = new Row(gap: Space.S2, cross: CrossAlign.Center, wrap: true, runGap: Space.S2);
        row.Add(new Text("Jump to", TypeRole.Caption, theme.TextMuted, maxLines: 1));
        foreach (var (fragment, label) in Sections) row.Add(JumpLink(theme, fragment, label));
        return row;
    }

    // Padding rather than a fixed height: a hugging row inside a fixed-height Box sits at its TOP,
    // and this pill has nothing to centre against, so the question is never asked.
    private static VisualNode JumpLink(IAppTheme theme, string fragment, string label) =>
        new Link($"#{fragment}", new Box(new BoxStyle
        {
            Padding = EdgeInsets.Symmetric(Space.S3, Space.S1),
            Background = theme.SurfaceSubtle,
            CornerRadius = new CornerRadii(Sizing.Radius(SizeVariant.Small)),
            Hover = new StyleDiff { Background = theme.Border },
            Transition = TransitionSpec.Of(StyleChannels.Colors, Motion.Press),
        }, new Text(label, TypeRole.Label, theme.TextSecondary, maxLines: 1)));

    /// <summary><paramref name="bookmark"/> is the name the "Jump to" row points at. It belongs to
    /// the CARD rather than the chart: landing on the title is landing on the thing.</summary>
    private static VisualNode Card(IAppTheme theme, string bookmark, VisualNode chart) =>
        new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
            Padding = EdgeInsets.All(Space.S4),
        }, chart)
        { Bookmark = bookmark };
}
