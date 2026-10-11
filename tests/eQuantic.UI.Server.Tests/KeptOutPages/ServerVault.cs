using eQuantic.UI.Primitives;
using StatelessComponent = eQuantic.UI.Primitives.StatelessComponent;

namespace eQuantic.UI.Server.Tests.KeptOutPages;

/// <summary>A server-only class with a page declared inside it, which eqc writes no module for, so the
/// server publishes no route to it (#584).</summary>
[ServerOnly]
public sealed class ServerVault
{
    [Page("/kept-out/vault")]
    public sealed class Door : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("the vault", TypeRole.Heading);
    }
}
