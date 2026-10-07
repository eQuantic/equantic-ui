using eQuantic.UI.Primitives;
using StatelessComponent = eQuantic.UI.Primitives.StatelessComponent;

namespace eQuantic.UI.Server.Tests.KeptOutPages;

/// <summary>A class that crosses, with a page declared inside it, which the route table names by its
/// twin (#584).</summary>
public sealed class OpenShelf
{
    [Page("/kept-out/open")]
    public sealed class Gate : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("the open shelf", TypeRole.Heading);
    }
}
