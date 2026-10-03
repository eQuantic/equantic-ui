using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Generators;

/// <summary>
/// A use of a server value the projection cannot follow, which fails the build (EQ2114): where it is,
/// the page and the value it concerns, the expression, and why the build cannot tell what of the value
/// the browser needs there.
/// </summary>
internal sealed class BoundaryStop
{
    public BoundaryStop(Location location, string page, string value, string expression, string reason)
    {
        Location = location; Page = page; Value = value; Expression = expression; Reason = reason;
    }

    public Location Location { get; }
    public string Page { get; }
    public string Value { get; }
    public string Expression { get; }
    public string Reason { get; }
}
