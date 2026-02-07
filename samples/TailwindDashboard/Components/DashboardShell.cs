using eQuantic.UI.Core;
using eQuantic.UI.Components;
using eQuantic.UI.Components.Layout;

namespace TailwindDashboard.Components;

public class DashboardShell : StatelessComponent
{
    public string? PageTitle { get; set; }
    public string? ActivePath { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var contentBox = new Box
        {
            ClassName = "flex-1 p-6 md:p-8",
            Children = {
                new Heading(PageTitle ?? "", 1) { ClassName = "text-2xl font-bold tracking-tight mb-6" }
            }
        };
        
        foreach (var child in Children)
        {
            contentBox.Children.Add(child);
        }

        return new Box
        {
            ClassName = "flex min-h-screen w-full bg-background font-sans antialiased",
            Children = {
                new AppSidebar { ActivePath = ActivePath },
                new Box {
                    ClassName = "flex-1 flex flex-col min-h-screen",
                    Children = {
                        // Topbar
                        new Box {
                            ClassName = "flex h-[60px] items-center gap-4 border-b border-border bg-background/95 px-6 backdrop-blur supports-[backdrop-filter]:bg-background/60 sticky top-0 z-30",
                            Children = {
                                new Link {
                                    Href = "#",
                                    ClassName = "lg:hidden font-semibold flex items-center gap-2",
                                    Children = { new Text("💠") { ClassName = "h-6 w-6" } }
                                },
                                new Box {
                                    ClassName = "w-full flex-1",
                                    Children = {
                                        new DynamicElement {
                                            TagName = "form",
                                            Children = {
                                                new Box {
                                                    ClassName = "relative flex items-center",
                                                    Children = {
                                                        new Text("🔍") { ClassName = "absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" },
                                                        new DynamicElement {
                                                            TagName = "input",
                                                            ClassName = "w-full appearance-none bg-muted/40 pl-8 shadow-none rounded-md border border-input h-9 text-sm focus:outline-none focus:ring-1 focus:ring-ring",
                                                            CustomAttributes = { ["placeholder"] = "Search components...", ["type"] = "search" }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                                new Box {
                                    ClassName = "flex items-center gap-4",
                                    Children = {
                                        new Button {
                                            Text = "🔔",
                                            Variant = eQuantic.UI.Core.Theme.Types.Variant.Ghost,
                                            ClassName = "rounded-full w-8 h-8 p-0"
                                        },
                                        new Box {
                                            ClassName = "h-8 w-8 rounded-full bg-muted border border-border flex items-center justify-center font-bold text-xs",
                                            Children = { new Text("EM") }
                                        }
                                    }
                                }
                            }
                        },
                        contentBox
                    }
                }
            }
        };
    }
}
