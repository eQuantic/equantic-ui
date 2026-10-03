using eQuantic.UI.Primitives;

namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>A page that hands the identity to a child, whose reads are what crosses.</summary>
[Page("/crossing-account")]
public sealed class CrossingAccountPage(CrossingIdentity identity) : StatelessComponent
{
    public override VisualNode Build(ComponentContext context) => new CrossingBadge(identity);
}
