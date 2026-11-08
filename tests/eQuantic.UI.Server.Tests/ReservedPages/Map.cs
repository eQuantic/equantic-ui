using eQuantic.UI.Primitives;
using StatelessComponent = eQuantic.UI.Primitives.StatelessComponent;

namespace eQuantic.UI.Server.Tests.ReservedPages;

/// <summary>A page named like a global the emitted code reads, whose module eqc writes as <c>Map$</c>
/// (#934, #890), with an action the module calls by that name.</summary>
[Page("/reserved/map")]
public sealed class Map : StatelessComponent
{
    [ServerAction]
    public Task<int> Pins() => Task.FromResult(2);

    public override VisualNode Build(ComponentContext context) => new Text("the map", TypeRole.Heading);
}
