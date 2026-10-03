using eQuantic.UI.Primitives;

namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>Reads the title of options declared as the base type, while the container hands it the derived one.</summary>
[Page("/crossing-branded")]
public sealed class CrossingBrandedPage(CrossingBaseOptions options) : StatelessComponent
{
    public override VisualNode Build(ComponentContext context) => new Text($"brand {options.Title}", TypeRole.BodyM);
}
