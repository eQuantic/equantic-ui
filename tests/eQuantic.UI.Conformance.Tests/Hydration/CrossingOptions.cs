namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>Options the container hands a page: one member the page shows, and one it never should.</summary>
public sealed class CrossingOptions
{
    public string Title { get; init; } = "";
    public string ApiKey { get; init; } = "";
}
