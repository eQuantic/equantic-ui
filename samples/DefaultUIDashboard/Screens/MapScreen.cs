using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// A map whose regions are inputs (#674): each <see cref="VectorShape"/> of the <see cref="eQuantic.UI.Primitives.Drawing"/>
/// is pressed where it is painted, reached by Tab, pressed by Enter or Space, announced by its name and
/// stated as pressed when it is the one picked, and it lightens under the pointer. The regions share
/// their borders, so each one's box holds a corner of its neighbours: a press there that picks the
/// neighbour is the box being tested instead of the path.
/// </summary>
[Page("/map", Title = "Map — eQuantic Console")]
public sealed class MapScreen : StatefulComponent
{
    private static readonly (string Name, string Path)[] Regions =
    [
        ("Matosinhos", "M0 0 L120 0 L80 100 L0 140 Z"),
        ("Maia", "M120 0 L300 0 L300 90 L80 100 Z"),
        ("Porto", "M0 140 L80 100 L300 90 L300 200 L0 200 Z"),
    ];

    private string? _chosen;

    /// <summary>Whether the compact drawer is up — page state wherever the page is.</summary>
    private bool _navOpen;

    public override VisualNode Build(ComponentContext context) =>
        ConsoleShell.Frame(context.Theme, "/map", "Map", Content(context),
            _navOpen, () => SetState(() => _navOpen = !_navOpen));

    private VisualNode Content(ComponentContext context)
    {
        var theme = context.Theme;
        var shapes = new List<VectorShape>();
        foreach (var (name, path) in Regions)
        {
            var picked = name == _chosen;
            // The paint says which one is picked to the eye, and Selected says it to a reader. The map
            // keeps its own colours in both modes, as a printed map does, and so does its hover.
            var fill = picked ? Color.FromRgb(37, 99, 235) : Color.FromRgb(203, 213, 225);
            var hover = picked ? Color.FromRgb(59, 130, 246) : Color.FromRgb(148, 163, 184);
            shapes.Add(new VectorShape(path, VectorPaint.Solid(fill), VectorPaint.Solid(Color.FromRgb(255, 255, 255)), 2)
            {
                Label = name,
                OnPressed = () => SetState(() => _chosen = name),
                Selected = picked,
                Hover = new StyleDiff { Background = new ColorToken(hover) },
            });
        }

        var column = new Column(gap: Space.S4) { Padding = EdgeInsets.All(Space.S6) };
        column.Add(new Text("Pick a concelho", TypeRole.Heading, theme.TextPrimary));
        column.Add(new Drawing(new VectorDrawing(0, 0, 300, 200, shapes), 480, label: "Porto district"));
        column.Add(new Text(_chosen is { } chosen ? $"Chosen: {chosen}" : "Nothing chosen yet",
            TypeRole.BodyM, theme.TextSecondary));
        return column;
    }
}
