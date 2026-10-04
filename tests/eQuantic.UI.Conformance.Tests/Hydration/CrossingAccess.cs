namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>A flags enum, which the browser holds as its number.</summary>
[System.Flags]
public enum CrossingAccess
{
    None = 0,
    Read = 1,
    Write = 2,
}
