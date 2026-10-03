namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>
/// A service the container hands a page, registered as a class: the browser has no way to build one,
/// and its <see cref="Authority"/> is what a login page once put into its HTML (#510).
/// </summary>
public sealed class CrossingIdentity
{
    public string Authority { get; init; } = "";
    public string DisplayName { get; init; } = "";
}
