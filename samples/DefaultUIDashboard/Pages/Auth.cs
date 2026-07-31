using DefaultUIDashboard.Components;
using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using StatelessComponent = eQuantic.UI.Core.StatelessComponent;

namespace DefaultUIDashboard.Pages;

/// <summary>
/// Route-guard demo (target route), migrated to the write-once library. The client guard
/// (window.__eqGuards, registered in Program.cs) gates <c>/admin</c>; the sign in/out actions stay
/// plain anchors whose query (<c>?login=1</c> / <c>?logout=1</c>) the guard interprets — no
/// app-level JavaScript. Pair with server-side <c>[Authorize]</c> for direct loads.
/// </summary>
[Page("/admin", Title = "Admin")]
public class Admin : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        var shell = new SampleShell { ActivePath = "/admin" };
        shell.Children.Add(new VisualNodeComponent(new AdminView()));
        return shell;
    }
}

/// <summary>The admin body as a WRITE-ONCE component (theme-aware sign-out link).</summary>
public class AdminView : eQuantic.UI.Primitives.StatelessComponent
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

/// <summary>
/// The guard's redirect destination. "Sign in" is a plain anchor to <c>/admin?login=1</c>; the guard
/// reads the query, flips the demo's auth flag, then lets the navigation continue.
/// </summary>
[Page("/login", Title = "Sign in")]
public class Login : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        var shell = new SampleShell { ActivePath = "/admin" };
        shell.Children.Add(new VisualNodeComponent(new LoginView()));
        return shell;
    }
}

/// <summary>The login body as a WRITE-ONCE component — the CTA is a Link with Box chrome
/// (button-look, anchor semantics), colors from the active theme.</summary>
public class LoginView : eQuantic.UI.Primitives.StatelessComponent
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
