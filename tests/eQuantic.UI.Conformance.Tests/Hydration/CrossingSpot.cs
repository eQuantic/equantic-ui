namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>A struct with a public field, which the wire does not carry, on <see cref="CrossingPrivateStatePage"/>.</summary>
public struct CrossingSpot
{
    public int X;

    public int Y { get; set; }
}
