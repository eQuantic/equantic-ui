using eQuantic.UI.Core;
using eQuantic.UI.Components;
using eQuantic.UI.Components.Layout;

namespace TailwindDashboard.Components;

public class SidebarItem : StatelessComponent
{
    public string? Text { get; set; }
    public string? Icon { get; set; }
    public string? Href { get; set; }
    public bool IsActive { get; set; }

    public override IComponent Build(RenderContext context)
    {
        return new Link
        {
            Href = Href ?? "#",
            ClassName = $"flex items-center gap-3 rounded-lg px-3 py-2 transition-all hover:text-zinc-900 group {(IsActive ? "bg-zinc-100 text-zinc-900 dark:bg-zinc-800 dark:text-zinc-50" : "text-zinc-500 hover:bg-zinc-100 dark:text-zinc-400 dark:hover:bg-zinc-800 dark:hover:text-zinc-50")}".Trim(),
            Children = {
                !string.IsNullOrEmpty(Icon) ? new Text(Icon) { ClassName = "h-4 w-4" } : new NullComponent(),
                new Text(Text ?? "") { ClassName = "text-sm font-medium" }
            }
        };
    }
}

public class Sidebar : StatelessComponent
{
    public string? ActivePath { get; set; }

    public override IComponent Build(RenderContext context)
    {
        return new Box
        {
            ClassName = "hidden border-r bg-zinc-50/40 lg:block dark:bg-zinc-950/40 w-[280px] h-screen fixed sticky top-0",
            Children = {
                new Box {
                    ClassName = "flex h-full max-h-screen flex-col gap-2",
                    Children = {
                        new Box {
                            ClassName = "flex h-[60px] items-center border-b px-6",
                            Children = {
                                new Link {
                                    Href = "/",
                                    ClassName = "flex items-center gap-2 font-semibold",
                                    Children = {
                                        new Text("💠") { ClassName = "h-6 w-6" },
                                        new Text("Tailwind UI")
                                    }
                                }
                            }
                        },
                        new Box {
                            ClassName = "flex-1 overflow-auto py-2",
                            Children = {
                                new DynamicElement {
                                    TagName = "nav",
                                    ClassName = "grid items-start px-4 text-sm font-medium",
                                    Children = {
                                        new SidebarItem { Text = "Dashboard", Icon = "🏠", Href = "/", IsActive = ActivePath == "/" },
                                        new SidebarItem { Text = "Tasks", Icon = "📋", Href = "/tasks", IsActive = ActivePath == "/tasks" },
                                        new SidebarItem { Text = "Showcase", Icon = "✨", Href = "/showcase", IsActive = ActivePath == "/showcase" },
                                        new SidebarItem { Text = "Analytics", Icon = "📈", Href = "/analytics", IsActive = ActivePath == "/analytics" }
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
