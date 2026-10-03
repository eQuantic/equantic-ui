using System.Collections.Generic;

namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>
/// A service holding the shapes a value cannot cross whole as: structs, a nullable one with and without
/// a value, bytes the serializer writes as base64, and a list a pattern reads beneath.
/// </summary>
public sealed class CrossingReport
{
    public CrossingTotals Totals { get; init; }
    public CrossingTotals? Maybe { get; init; }
    public CrossingTotals? Never { get; init; }
    public CrossingPosition Position { get; init; }
    public byte[] Bytes { get; init; } = [];
    public List<string> Tags { get; init; } = new();
}
