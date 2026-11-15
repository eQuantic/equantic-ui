namespace eQuantic.UI.Primitives;

/// <summary>
/// Where one item of a tree stands: how deep (<see cref="Level"/>, 1 for a root), and where among its
/// siblings (<see cref="PositionInSet"/> of <see cref="SizeOfSet"/>). All three are counted from one,
/// with the names and the base of .NET's own <c>AutomationProperties.Level</c>, <c>PositionInSet</c>
/// and <c>SizeOfSet</c>, which say the same three facts to UI Automation; ARIA's <c>aria-level</c>,
/// <c>aria-posinset</c> and <c>aria-setsize</c> are the same numbers.
/// <para>
/// A tree's rows are a flat list of its VISIBLE items, on every target, so nothing a realizer walks can
/// tell a reader how deep an item is or how many siblings it has: the item states them (#1529).
/// </para>
/// </summary>
[ZeroConstructs("value-types.ts: a bare `new TreePlace()` is three zeros, the struct's default.")]
public readonly record struct TreePlace(int Level, int PositionInSet, int SizeOfSet);
