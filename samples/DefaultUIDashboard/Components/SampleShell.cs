using eQuantic.UI.Core;

namespace DefaultUIDashboard.Components;

/// <summary>
/// The demo's PERSISTENT shell: a slim top nav of plain anchors (SPA links the router intercepts)
/// over the page body. Core-authored with <see cref="DynamicElement"/> — no component library, no
/// stylesheet dependency (inline styles read the UseTheme token variables, so the chrome follows
/// the selected theme). Every page renders the same shell shape, which is what lets the
/// reconcile-on-navigate keep its DOM (and scroll) across page swaps.
/// </summary>
public class SampleShell : StatelessComponent
{
    public string ActivePath { get; set; } = "/";
    public List<IComponent> Children { get; } = new();

    private sealed record NavLink(string Href, string Label);

    private static readonly List<NavLink> Nav =
    [
        new NavLink("/", "Showroom"),
        new NavLink("/shared", "Components"),
        new NavLink("/counter", "Counter"),
        new NavLink("/users", "Users"),
        new NavLink("/admin", "Admin"),
    ];

    public override IComponent Build(RenderContext context)
    {
        var bar = new DynamicElement
        {
            TagName = "nav",
            CustomAttributes = new Dictionary<string, string>
            {
                ["style"] = "display:flex;align-items:center;gap:4px;padding:10px 16px;" +
                            "border-bottom:1px solid var(--eq-color-border);" +
                            "background:var(--eq-color-surface);position:sticky;top:0;z-index:50;",
            },
        };

        var brand = new DynamicElement
        {
            TagName = "a",
            InnerText = "eQuantic.UI",
            CustomAttributes = new Dictionary<string, string>
            {
                ["href"] = "/",
                ["style"] = "font-weight:800;margin-right:12px;text-decoration:none;color:var(--eq-color-text-primary);",
            },
        };
        bar.Children.Add(brand);

        foreach (var link in Nav)
        {
            var active = link.Href == ActivePath;
            bar.Children.Add(new DynamicElement
            {
                TagName = "a",
                InnerText = link.Label,
                CustomAttributes = new Dictionary<string, string>
                {
                    ["href"] = link.Href,
                    ["style"] = "padding:6px 12px;border-radius:999px;text-decoration:none;font-size:14px;" +
                                (active
                                    ? "background:var(--eq-color-primary-subtle);color:var(--eq-color-primary-on-subtle);font-weight:600;"
                                    : "color:var(--eq-color-text-secondary);"),
                },
            });
        }

        var root = new DynamicElement { TagName = "div" };
        root.Children.Add(bar);
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
