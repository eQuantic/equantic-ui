namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>An enum over a long that is not a flags one, which the browser holds as a BigInt where no member
/// names a value.</summary>
public enum CrossingTier : long
{
    Low = 1,
    High = 2,
}
