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
///     values: ValueAxis(format: "C0"))
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

    public static ChartSeries ChartSeries(string name, IReadOnlyList<double> values, int slot = -1) =>
        new(name, values, slot);

    public static CategoryAxis CategoryAxis(IReadOnlyList<string> categories, string? title = null) =>
        new(categories, title);

    public static ValueAxis ValueAxis(string? title = null, double? min = null, double? max = null,
        string format = "N0", int ticks = 5) =>
        new(title, min, max, format, ticks);
}
