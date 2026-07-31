using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
// The alias NAME is what the emitter's base-swap matches — Primitives StatelessComponent under its
// own name (the SharedCounterPage pattern). This file holds every WRITE-ONCE view of the sample.
using StatelessComponent = eQuantic.UI.Primitives.StatelessComponent;

namespace DefaultUIDashboard.Components;

/// <summary>The write-once nav bar: brand + pill links, colors and shape from the ACTIVE theme.
/// Real <see cref="Link"/> nodes — SSR-crawlable anchors the SPA router intercepts; the SAME bar
/// would rasterize on Photon with taps routed through the host's navigation seam.</summary>
public class ShellBar : StatelessComponent
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

/// <summary>The user-detail body — theme-aware back link.</summary>
public class UserDetailView : StatelessComponent
{
    public string Id { get; init; } = "?";
    public string Role { get; init; } = "member";

    public override VisualNode Build(ComponentContext context)
    {
        var body = new Column(gap: Space.S3) { Cross = CrossAlign.Center, Padding = EdgeInsets.All(Space.S6) };
        body.Add(new Avatar(Id, SizeVariant.XLarge, name: $"User {Id}"));
        body.Add(new Text($"User #{Id}", TypeRole.Title));
        body.Add(new Text($"Route param id = {Id} · query role = {Role}", TypeRole.Caption));
        body.Add(new Link("/users", new Text("← Back to users", TypeRole.Label, context.Theme.LinkColor)));
        return new Card(body, CardKind.Outlined) { Width = SizeValue.Fill };
    }
}

/// <summary>The admin body (theme-aware sign-out link).</summary>
public class AdminView : StatelessComponent
{
    public override VisualNode Build(ComponentContext context)
    {
        var body = new Column(gap: Space.S3) { Padding = EdgeInsets.All(Space.S5) };
        body.Add(new Text("Admin area", TypeRole.Title));
        body.Add(new Banner(Variant.Success, "You are signed in",
            "The route guard let this navigation through."));
        body.Add(new Link("/login?logout=1", new Text("Sign out", TypeRole.Label, context.Theme.LinkColor)));
        return new Card(body, CardKind.Outlined) { Width = SizeValue.Fill };
    }
}

/// <summary>The login body — the CTA is a Link with Box chrome (button look, anchor semantics).</summary>
public class LoginView : StatelessComponent
{
    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var body = new Column(gap: Space.S3) { Cross = CrossAlign.Center, Padding = EdgeInsets.All(Space.S6) };
        body.Add(new Text("Sign in required", TypeRole.Title));
        body.Add(new Text(
            "The route guard redirected you here because /admin is protected.", TypeRole.BodyM));
        body.Add(new Link("/admin?login=1", new Box(new BoxStyle
        {
            Padding = EdgeInsets.Symmetric(Space.S5, Space.S3),
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
            Background = theme.Colors(Variant.Primary).Base,
        }, new Text("Sign in & continue →", TypeRole.Label, theme.Colors(Variant.Primary).OnBase))));
        return new Card(body, CardKind.Outlined) { Width = SizeValue.Fill };
    }
}
