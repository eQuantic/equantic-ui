using eQuantic.UI.Core;
using eQuantic.UI.Web.Components;
using eQuantic.UI.Web.Components.Layout;
using eQuantic.UI.Lucide;

namespace TailwindDashboard.Components;

public class SidebarItem : StatelessComponent
{
    public string? Text { get; set; }
    public IComponent? Icon { get; set; }
    public string? Href { get; set; }
    public bool IsActive { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var baseClass = "flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors";
        var stateClass = IsActive
            ? "bg-primary/10 text-primary"
            : "text-muted-foreground hover:bg-accent hover:text-accent-foreground";

        return new Link
        {
            Href = Href ?? "#",
            ClassName = $"{baseClass} {stateClass}",
            Children = {
                Icon != null
                    ? new Box {
                        ClassName = "shrink-0",
                        Children = { Icon }
                    }
                    : new NullComponent(),
                new Text(Text ?? "")
            }
        };
    }
}

public class AppSidebar : StatelessComponent
{
    public string? ActivePath { get; set; }

    public override IComponent Build(RenderContext context)
    {
        return new Box
        {
            ClassName = "hidden border-r border-border bg-card lg:block w-[260px] h-screen sticky top-0",
            Children = {
                new Box {
                    ClassName = "flex h-full max-h-screen flex-col",
                    Children = {
                        // Logo header
                        new Box {
                            ClassName = "flex h-14 items-center border-b border-border px-5",
                            Children = {
                                new Link {
                                    Href = "/",
                                    ClassName = "flex items-center gap-2.5 font-semibold text-foreground",
                                    Children = {
                                        new Box {
                                            ClassName = "flex h-7 w-7 items-center justify-center rounded-md bg-primary text-primary-foreground text-xs font-bold",
                                            Children = { new Text("eQ") }
                                        },
                                        new Text("Tailwind UI") { ClassName = "text-sm" }
                                    }
                                }
                            }
                        },
                        // Navigation
                        new Box {
                            ClassName = "flex-1 overflow-auto px-3 py-4",
                            Children = {
                                new DynamicElement {
                                    TagName = "nav",
                                    ClassName = "flex flex-col gap-1",
                                    Children = {
                                        // Main section
                                        new Text("Menu") { ClassName = "px-3 pb-2 text-[11px] font-semibold text-muted-foreground/70 uppercase tracking-widest" },
                                        new SidebarItem { Text = "Dashboard", Icon = LucideIcons.LayoutDashboard(size: 18, strokeWidth: 1.75), Href = "/", IsActive = ActivePath == "/" },
                                        new SidebarItem { Text = "Analytics", Icon = LucideIcons.TrendingUp(size: 18, strokeWidth: 1.75), Href = "/analytics", IsActive = ActivePath == "/analytics" },
                                        new SidebarItem { Text = "Tasks", Icon = LucideIcons.ListTodo(size: 18, strokeWidth: 1.75), Href = "/tasks", IsActive = ActivePath == "/tasks" },

                                        // Components section
                                        new Box { ClassName = "mt-6 mb-1 border-t border-border/50 pt-4" },
                                        new Text("Components") { ClassName = "px-3 pb-2 text-[11px] font-semibold text-muted-foreground/70 uppercase tracking-widest" },
                                        new SidebarItem { Text = "Buttons", Icon = LucideIcons.MousePointerClick(size: 18, strokeWidth: 1.75), Href = "/components/buttons", IsActive = ActivePath == "/components/buttons" },
                                        new SidebarItem { Text = "Inputs", Icon = LucideIcons.TextCursorInput(size: 18, strokeWidth: 1.75), Href = "/components/inputs", IsActive = ActivePath == "/components/inputs" },
                                        new SidebarItem { Text = "Display", Icon = LucideIcons.Eye(size: 18, strokeWidth: 1.75), Href = "/components/display", IsActive = ActivePath == "/components/display" },
                                        new SidebarItem { Text = "Surfaces", Icon = LucideIcons.Layers3(size: 18, strokeWidth: 1.75), Href = "/components/surfaces", IsActive = ActivePath == "/components/surfaces" },
                                        new SidebarItem { Text = "Feedback", Icon = LucideIcons.MessageCircleWarning(size: 18, strokeWidth: 1.75), Href = "/components/feedback", IsActive = ActivePath == "/components/feedback" },
                                        new SidebarItem { Text = "Overlays", Icon = LucideIcons.PanelsTopLeft(size: 18, strokeWidth: 1.75), Href = "/components/overlays", IsActive = ActivePath == "/components/overlays" },
                                        new SidebarItem { Text = "Navigation", Icon = LucideIcons.Compass(size: 18, strokeWidth: 1.75), Href = "/components/navigation", IsActive = ActivePath == "/components/navigation" },
                                        new SidebarItem { Text = "Layout", Icon = LucideIcons.LayoutGrid(size: 18, strokeWidth: 1.75), Href = "/components/layout", IsActive = ActivePath == "/components/layout" }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }
}
