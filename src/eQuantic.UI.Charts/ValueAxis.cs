using System.Globalization;

namespace eQuantic.UI.Charts;

/// <summary>
/// The ONE value axis. There is no second: two measures of different scale are two charts, or one
/// indexed to a common base — a dual axis invents a correlation the data does not carry.
/// </summary>
/// <param name="Title">The axis title.</param>
/// <param name="Min">A fixed lower bound; the data's own (never above zero) when null.</param>
/// <param name="Max">A fixed upper bound; the data's own (never below zero) when null.</param>
/// <param name="Format">The .NET format the ticks and the tooltip use, applied in the request's
/// culture — <c>N0</c>, <c>N2</c>, <c>P0</c>, <c>C0</c> are the ones that cross to every target.</param>
/// <param name="Ticks">How many tick labels to aim for; the scale snaps to clean steps.</param>
public sealed record ValueAxis(string? Title = null, double? Min = null, double? Max = null,
    string Format = "N0", int Ticks = 5)
{
    /// <summary>A value as the axis shows it.</summary>
    public string Label(double value) => value.ToString(Format, CultureInfo.CurrentCulture);
}
