using eQuantic.UI.Primitives;
using StatelessComponent = eQuantic.UI.Primitives.StatelessComponent;

namespace eQuantic.UI.Server.Tests.KeptOutPages;

/// <summary>A class the runtime provides, with a component declared inside it, which eqc writes no
/// module for: the runtime carries its owner, so a page mapped to it would have none to load (#584).</summary>
[RuntimeProvided]
public sealed class RuntimeShelf
{
    public sealed class Gate : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("the runtime's shelf", TypeRole.Heading);
    }
}
