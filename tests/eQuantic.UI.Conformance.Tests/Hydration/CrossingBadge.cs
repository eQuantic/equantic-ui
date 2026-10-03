using eQuantic.UI.Primitives;

namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>A child that shows the name of the identity its page passes it.</summary>
public sealed class CrossingBadge(CrossingIdentity identity) : StatelessComponent
{
    public override VisualNode Build(ComponentContext context) => new Text($"badge {identity.DisplayName}", TypeRole.BodyM);
}
