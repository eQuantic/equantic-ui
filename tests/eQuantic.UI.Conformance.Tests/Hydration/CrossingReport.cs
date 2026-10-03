using System.Collections.Generic;

namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>
/// A service holding values that cross as their reads (structs, a nullable one with and without a
/// value) and values that cross whole (a list a pattern reads beneath, a dictionary and a list of
/// longs, which the browser rebuilds as its own collections).
/// </summary>
public sealed class CrossingReport
{
    public CrossingTotals Totals { get; init; }
    public CrossingTotals? Maybe { get; init; }
    public CrossingTotals? Never { get; init; }
    public CrossingPosition Position { get; init; }
    public List<string> Tags { get; init; } = new();
    public Dictionary<string, long> Prices { get; init; } = new();
    public List<long> Scores { get; init; } = new();
}
