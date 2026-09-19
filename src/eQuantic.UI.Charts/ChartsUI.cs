namespace eQuantic.UI.Charts;

/// <summary>
/// The DECLARATIVE surface of the chart library — every chart and every chart value as a factory
/// named exactly like its type, mirroring its constructor parameter for parameter, so a screen reads
/// without a single <c>new</c>. The SDK puts it in scope beside <c>eQuantic.UI.Components.UI</c>
/// (<c>using static eQuantic.UI.Charts.ChartsUI;</c>), and a dashboard is:
/// <code>
/// BarChart(title: "Revenue",
///     series: [ ChartSeries("2025", [12, 18, 9]), ChartSeries("2026", [15, 21, 14]) ],
///     categories: CategoryAxis(["Q1", "Q2", "Q3"]),
///     values: ValueAxis(Format: "C0"))
/// </code>
/// </summary>
public static class ChartsUI
{
    // `Charts.BarChart` and not `BarChart`: inside this class the bare name is the FACTORY below,
    // so the default has to name the type through the enclosing namespace. Every factory that
    // borrows a constant from the type it builds reads this way.
    public static BarChart BarChart(IReadOnlyList<ChartSeries> series, CategoryAxis categories,
        ValueAxis? values = null, BarLayout layout = BarLayout.Grouped,
        ChartOrientation orientation = ChartOrientation.Vertical, string? title = null,
        string? subtitle = null, float plotHeight = Charts.BarChart.DefaultPlotHeight) =>
        new(series, categories, values, layout, orientation, title, subtitle, plotHeight);

    // The three below take PASCAL-CASE parameters, alone on either surface, because they mirror
    // POSITIONAL RECORDS: a record's positional parameters ARE its properties, so `Name` is the
    // name `new ChartSeries(Name: "2026")` takes, and a camel-cased factory parameter would be a
    // second spelling of the same argument — the one thing the mirror exists to prevent. Every
    // value record in the SDK (NavItem, DialogAction, GridTrack…) is named this way already; a
    // consumer moving between the two forms should not have to recase anything.
    public static ChartSeries ChartSeries(string Name, IReadOnlyList<double> Values, int Slot = -1) =>
        new(Name, Values, Slot);

    public static CategoryAxis CategoryAxis(IReadOnlyList<string> Categories, string? Title = null) =>
        new(Categories, Title);

    public static ValueAxis ValueAxis(string? Title = null, double? Min = null, double? Max = null,
        string Format = "N0", int Ticks = 5) =>
        new(Title, Min, Max, Format, Ticks);
}
