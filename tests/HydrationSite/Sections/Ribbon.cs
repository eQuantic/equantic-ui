using eQuantic.UI.Primitives;

namespace HydrationSite.Sections;

/// <summary>
/// A component two shelf pages reach only through <see cref="Showcase"/>, and the ribbon page reaches itself
/// (#1804). The bundler puts it in a chunk of its own, and each shelf page imports that chunk for its side
/// effects alone, with no space after <c>import</c>: <c>import"./ShelfOnePage-….js"</c>.
/// </summary>
public sealed class Ribbon(string label) : StatelessComponent
{
    public string Label { get; } = label;

    public override VisualNode Build(ComponentContext context) => Text(Label, TypeRole.Label);
}
