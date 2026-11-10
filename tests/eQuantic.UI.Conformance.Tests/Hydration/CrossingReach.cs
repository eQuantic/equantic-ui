namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>A flags enum over a long, which the browser holds as a BigInt: its top flag is past 2^53.</summary>
[System.Flags]
public enum CrossingReach : long
{
    None = 0,
    Near = 1,
    Far = 1L << 62,
}
