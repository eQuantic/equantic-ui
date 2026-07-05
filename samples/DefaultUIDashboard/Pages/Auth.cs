using eQuantic.UI.Core;
using eQuantic.UI.Web.Components;
using eQuantic.UI.Web.Components.Layout;
using DefaultUIDashboard.Components;

namespace DefaultUIDashboard.Pages;

/// <summary>
/// Route-guard demo (target route). A client navigation guard (registered in <c>Program.cs</c> via the
/// documented <c>window.__eqGuards</c> hook) gates this route: clicking <b>Admin</b> while "signed out"
/// is cancelled and redirected to <c>/login</c>. After signing in, the same click is allowed.
/// The whole flow is authored in C# — the login/out actions are plain <see cref="Link"/>s whose query
/// (<c>?login=1</c> / <c>?logout=1</c>) the guard interprets; no app-level JavaScript.
///
/// Note: a client guard protects in-app (SPA) navigation. Pair it with server-side
/// <c>[Authorize]</c> to also protect a direct page load / refresh.
/// </summary>
[Page("/admin", Title = "Admin")]
public class Admin : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new DefaultDashboardShell
        {
            ActivePath = "/admin",
            Children = {
                new Box {
                    ClassName = "eq-max-w-lg eq-mx-auto eq-mt-20 eq-surface-subtle eq-border eq-rounded-2xl eq-p-10",
                    Children = {
                        new Box {
                            ClassName = "eq-flex eq-items-center eq-gap-3 eq-mb-6",
                            Children = {
                                new Box { ClassName = "eq-w-3 eq-h-3 eq-rounded-full eq-bg-success", Children = { } },
                                new Heading("Admin area", 1) { ClassName = "eq-text-2xl eq-font-bold" }
                            }
                        },
                        new Text("You are signed in — the route guard let this navigation through.")
                        {
                            ClassName = "eq-text-secondary eq-mb-8 eq-block"
                        },
                        new Link
                        {
                            Href = "/login?logout=1",
                            ClassName = "eq-btn-outline eq-px-6 eq-py-2 eq-rounded-lg eq-inline-block",
                            Children = { new Text("Sign out") }
                        }
                    }
                }
            }
        };
    }
}

/// <summary>
/// The guard's redirect destination. "Sign in" is a plain link to <c>/admin?login=1</c>; the guard reads
/// the query, flips the demo's auth flag, then lets the navigation continue to <c>/admin</c>.
/// </summary>
[Page("/login", Title = "Sign in")]
public class Login : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new DefaultDashboardShell
        {
            ActivePath = "/admin",
            Children = {
                new Box {
                    ClassName = "eq-max-w-md eq-mx-auto eq-mt-24 eq-surface-subtle eq-border eq-rounded-2xl eq-p-10 eq-text-center",
                    Children = {
                        new Heading("Sign in required", 1) { ClassName = "eq-text-2xl eq-font-bold eq-mb-2" },
                        new Text("The route guard redirected you here because /admin is protected.")
                        {
                            ClassName = "eq-text-secondary eq-text-sm eq-mb-8 eq-block"
                        },
                        new Link
                        {
                            Href = "/admin?login=1",
                            ClassName = "eq-btn-primary eq-px-8 eq-py-3 eq-rounded-lg eq-inline-block eq-font-medium",
                            Children = { new Text("Sign in & continue →") }
                        }
                    }
                }
            }
        };
    }
}
