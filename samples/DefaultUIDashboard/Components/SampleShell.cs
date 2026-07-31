using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using StatelessComponent = eQuantic.UI.Core.StatelessComponent;

namespace DefaultUIDashboard.Components;

/// <summary>
/// The demo's PERSISTENT shell: a WRITE-ONCE nav bar (real <see cref="Link"/> nodes — SSR-crawlable
/// anchors the SPA router intercepts; the SAME bar would rasterize on Photon with taps routed
/// through the host's navigation seam) over the page body. Every page renders the same shell shape,
/// which is what lets reconcile-on-navigate keep the bar's DOM across page swaps. The body wrapper
/// is the one CORE-level structural element (<see cref="DynamicElement"/> — the escape hatch), since
/// it hosts arbitrary IComponent children.
/// </summary>
public class SampleShell : StatelessComponent
{
    public string ActivePath { get; set; } = "/";
    public List<IComponent> Children { get; } = new();

    public override IComponent Build(RenderContext context)
    {
        var root = new DynamicElement { TagName = "div" };
        root.Children.Add(new VisualNodeComponent(new ShellBar { ActivePath = ActivePath }));

        var body = new DynamicElement
        {
            TagName = "main",
            CustomAttributes = new Dictionary<string, string>
            {
                ["style"] = "max-width:720px;margin:0 auto;padding:24px 16px;",
            },
        };
        foreach (var child in Children) body.Children.Add(child);
        root.Children.Add(body);
        return root;
    }
}

/// <summary>The write-once nav bar: brand + pill links, colors and shape from the ACTIVE theme.</summary>
public class ShellBar : eQuantic.UI.Primitives.StatelessComponent
{
    private sealed record NavEntry(string Href, string Label);

    private static readonly List<NavEntry> Nav =
    [
        new NavEntry("/", "Showroom"),
        new NavEntry("/shared", "Components"),
        new NavEntry("/counter", "Counter"),
        new NavEntry("/users", "Users"),
        new NavEntry("/admin", "Admin"),
    ];

    public string ActivePath { get; init; } = "/";

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var bar = new Row(gap: Space.S1)
        {
            Cross = CrossAlign.Center,
            Width = SizeValue.Fill,
            Padding = EdgeInsets.Symmetric(Space.S4, Space.S2),
            Background = theme.Surface,
        };

        bar.Add(new Link("/", new Box(new BoxStyle { Padding = EdgeInsets.Symmetric(Space.S2, Space.S1) },
            new Text("eQuantic.UI", TypeRole.Label))));

        foreach (var entry in Nav)
        {
            var active = entry.Href == ActivePath;
            var pill = new Box(new BoxStyle
            {
                Padding = EdgeInsets.Symmetric(Space.S3, 6),
                CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
                Background = active ? theme.Colors(Variant.Primary).Subtle : null,
            }, new Text(entry.Label, TypeRole.Caption,
                active ? theme.Colors(Variant.Primary).OnSubtle : theme.TextSecondary));
            bar.Add(new Link(entry.Href, pill));
        }

        return bar;
    }
}
