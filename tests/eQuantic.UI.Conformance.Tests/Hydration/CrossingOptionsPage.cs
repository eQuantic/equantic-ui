using eQuantic.UI.Primitives;

namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>A page that keeps its options in a field and shows one member of them.</summary>
[Page("/crossing-options")]
public sealed class CrossingOptionsPage : StatelessComponent
{
    private readonly CrossingOptions _options;

    public CrossingOptionsPage(CrossingOptions options)
    {
        _options = options;
    }

    public override VisualNode Build(ComponentContext context) => new Text($"title {_options.Title}", TypeRole.BodyM);
}
