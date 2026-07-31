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

/// <summary>
/// The guard's redirect destination. "Sign in" is a write-once Link to <c>/admin?login=1</c>; the
/// guard reads the query, flips the demo's auth flag, then lets the navigation continue.
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
