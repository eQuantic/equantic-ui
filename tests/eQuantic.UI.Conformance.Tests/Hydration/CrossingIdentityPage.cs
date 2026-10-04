using eQuantic.UI.Primitives;

namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>
/// A page that reads only whether someone is signed in, and links by it: the decision the browser drew
/// the other branch of, because the router builds the page without the identity.
/// </summary>
[Page("/crossing-identity")]
public sealed class CrossingIdentityPage(CrossingIdentity? identity = null) : StatelessComponent
{
    public override VisualNode Build(ComponentContext context) =>
        new Link(identity is null ? "/signin" : "/account",
            new Text(identity is null ? "sign in" : "account", TypeRole.BodyM));
}
