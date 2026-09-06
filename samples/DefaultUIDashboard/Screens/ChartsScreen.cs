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

        var wide = new Row(gap: Space.S4, cross: CrossAlign.Start, wrap: true, runGap: Space.S4);
        wide.Add(new Flexible(Card(theme, BarChart(
            title: "Grouped",
            subtitle: "Three regions, side by side",
            series: Regions(),
            categories: CategoryAxis(Quarters),
            values: ValueAxis(format: "N0", title: "Revenue")))));
        wide.Add(new Flexible(Card(theme, BarChart(
            title: "Stacked",
            subtitle: "The same regions, one on top of another",
            series: Regions(),
            categories: CategoryAxis(Quarters),
            values: ValueAxis(format: "N0"),
            layout: BarLayout.Stacked))));
        page.Add(wide);

        page.Add(Card(theme, BarChart(
            title: "Horizontal",
            subtitle: "Long category names read best this way",
            series: [ChartSeries("Tickets closed", [42, 31, 57, 12, 26])],
            categories: CategoryAxis(["Billing", "Onboarding", "Performance", "Security", "Accessibility"]),
            values: ValueAxis(format: "N0"),
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

    private static VisualNode Card(IAppTheme theme, VisualNode chart) =>
        new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
            Padding = EdgeInsets.All(Space.S4),
        }, chart);
}
