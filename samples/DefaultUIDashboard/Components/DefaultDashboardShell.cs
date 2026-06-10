using eQuantic.UI.Core;
using eQuantic.UI.Components;
using eQuantic.UI.Components.Layout;

namespace DefaultUIDashboard.Components;

public class NavItem : StatelessComponent
{
    public string? Text { get; set; }
    public string? Href { get; set; }
    public bool IsActive { get; set; }

    public override IComponent Build(RenderContext context)
    {
        return new Link
        {
            Href = Href ?? "#",
            ClassName = $"eq-px-4 eq-py-2 eq-rounded-md eq-transition-colors {(IsActive ? "eq-btn-primary" : "eq-text-secondary hover:eq-text-primary hover:eq-surface-hover")}",
            Children = { new Text(Text ?? "") }
        };
    }
}

public class DefaultDashboardShell : StatelessComponent
{
    public string? ActivePath { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var navbar = new Box {
            ClassName = "eq-h-16 eq-border-b eq-flex eq-items-center eq-px-8 eq-justify-between eq-sticky eq-top-0 eq-surface-subtle eq-shadow-sm eq-z-50",
            Children = {
                new Link {
                    Href = "/",
                    ClassName = "eq-text-xl eq-font-bold eq-flex eq-items-center eq-gap-2",
                    Children = { 
                        new Box { ClassName = "eq-w-8 eq-h-8 eq-btn-primary eq-rounded-lg eq-flex eq-items-center eq-justify-center", Children = { new Text("Q") } },
                        new Text("Default UI Dashboard")
                    }
                },
                new Box {
                    ClassName = "eq-flex eq-items-center eq-gap-2",
                    Children = {
                        new NavItem { Text = "Dashboard", Href = "/", IsActive = ActivePath == "/" },
                        new NavItem { Text = "Counter", Href = "/counter", IsActive = ActivePath == "/counter" },
                        new NavItem { Text = "Users", Href = "/users", IsActive = ActivePath == "/users" },
                        new NavItem { Text = "Admin", Href = "/admin", IsActive = ActivePath == "/admin" },
                        new NavItem { Text = "Showcase", Href = "/showcase", IsActive = ActivePath == "/showcase" }
                    }
                },
                new Box {
                    ClassName = "eq-w-10 eq-h-10 eq-rounded-full eq-surface-hover eq-border"
                }
            }
        };

        var contentBox = new Box {
            ClassName = "eq-flex-1 eq-p-8",
        };
        foreach (var child in Children) contentBox.Children.Add(child);
        
        return new Box
        {
            ClassName = "eq-h-full eq-w-full eq-surface eq-text-primary eq-flex eq-flex-col",
            Children = {
                navbar,
                contentBox
            }
        };
    }
}
