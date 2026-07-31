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
        var body = new Column(gap: Space.S3) { Padding = EdgeInsets.All(Space.S5) };
        body.Add(new Text("Admin area", TypeRole.Title));
        body.Add(new Banner(Variant.Success, "You are signed in",
            "The route guard let this navigation through."));

        var signOut = new DynamicElement
        {
            TagName = "a",
            InnerText = "Sign out",
            CustomAttributes = new Dictionary<string, string>
            {
                ["href"] = "/login?logout=1",
                ["style"] = "display:inline-block;margin-top:8px;color:var(--eq-color-link);",
            },
        };

        var shell = new SampleShell { ActivePath = "/admin" };
        shell.Children.Add(new VisualNodeComponent(new Card(body, CardKind.Outlined) { Width = SizeValue.Fill }));
        shell.Children.Add(signOut);
        return shell;
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
        var body = new Column(gap: Space.S3) { Cross = CrossAlign.Center, Padding = EdgeInsets.All(Space.S6) };
        body.Add(new Text("Sign in required", TypeRole.Title));
        body.Add(new Text(
            "The route guard redirected you here because /admin is protected.", TypeRole.BodyM));

        var signIn = new DynamicElement
        {
            TagName = "a",
            InnerText = "Sign in & continue →",
            CustomAttributes = new Dictionary<string, string>
            {
                ["href"] = "/admin?login=1",
                ["style"] = "display:inline-block;margin-top:8px;padding:10px 20px;border-radius:10px;" +
                            "background:var(--eq-color-primary-base);color:var(--eq-color-primary-on);" +
                            "text-decoration:none;font-weight:600;",
            },
        };

        var shell = new SampleShell { ActivePath = "/admin" };
        shell.Children.Add(new VisualNodeComponent(new Card(body, CardKind.Outlined) { Width = SizeValue.Fill }));
        shell.Children.Add(signIn);
        return shell;
    }
}
