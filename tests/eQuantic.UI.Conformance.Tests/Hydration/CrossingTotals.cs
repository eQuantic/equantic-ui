namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>
/// A struct whose one public member is computed, so it has no settable member a twin would carry:
/// crossed whole, its long arrived as the string the server's serializer writes.
/// </summary>
public readonly struct CrossingTotals
{
    private readonly long _seed;

    public CrossingTotals(long seed) => _seed = seed;

    public long Count => _seed * 6;
}
