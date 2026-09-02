using eQuantic.UI.Components;
using eQuantic.UI.Primitives;

namespace EQuanticNativeApp.Screens;

/// <summary>The second destination — a field and the nothing-here state, which is the screen most
/// apps forget to design and every user reaches on their first visit.</summary>
public sealed class SearchScreen : StatefulComponent
{
    private string _query = "";

    public override VisualNode Build(ComponentContext context) =>
        Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S4),
        },
        new Column(gap: Space.S4) { Height = SizeValue.Fill }
            .With(SearchField(_query, value => SetState(() => _query = value), "Search"))
            .With(Flexible(EmptyState(Icon(Icons.Search),
                _query.Length == 0 ? "Start typing" : $"Nothing matches \"{_query}\"",
                "Wire this to a [ServerAction] on the web, or to your own service on the device."))));
}
