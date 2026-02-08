using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;
using eQuantic.UI.Components;
using eQuantic.UI.Components.Display;
using eQuantic.UI.Components.Layout;
using eQuantic.UI.Lucide;

namespace TailwindDashboard.Components;

public class DashboardShell : StatelessComponent
{
    public string? PageTitle { get; set; }
    public string? ActivePath { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var contentBox = new Box
        {
            ClassName = "flex-1 px-8 py-8 lg:px-10",
            Children = {
                // Page header with title
                new Box {
                    ClassName = "flex-1 p-8 pt-6",
                    Children = {
                        new Heading(PageTitle ?? "", 1) { ClassName = "text-3xl font-bold tracking-tight text-foreground" },
                        new CodeBlockScripts()
                    }
                }
            }
        };

        foreach (var child in Children)
        {
            contentBox.Children.Add(child);
        }

        return new Box
        {
            ClassName = "flex min-h-screen w-full bg-background font-sans antialiased text-foreground",
            Children = {
                new AppSidebar { ActivePath = ActivePath },
                new Box {
                    ClassName = "flex-1 flex flex-col min-h-screen",
                    Children = {
                        // Topbar
                        new Box {
                            ClassName = "flex h-14 items-center gap-4 border-b border-border bg-card/80 px-8 backdrop-blur-sm sticky top-0 z-30",
                            Children = {
                                // Mobile logo
                                new Link {
                                    Href = "#",
                                    ClassName = "lg:hidden font-semibold flex items-center gap-2",
                                    Children = {
                                        new Box {
                                            ClassName = "flex h-7 w-7 items-center justify-center rounded-md bg-primary text-primary-foreground text-xs font-bold",
                                            Children = { new Text("eQ") }
                                        }
                                    }
                                },
                                // Search
                                new Box {
                                    ClassName = "flex-1 max-w-md",
                                    Children = {
                                        new Box {
                                            ClassName = "relative",
                                            Children = {
                                                new Box {
                                                    ClassName = "absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground/60",
                                                    Children = { LucideIcons.Search(size: 16, strokeWidth: 1.75) }
                                                },
                                                new DynamicElement {
                                                    TagName = "input",
                                                    ClassName = "w-full h-9 rounded-lg border border-border/60 bg-background/50 pl-12 pr-4 text-sm text-foreground placeholder:text-muted-foreground/60 focus:outline-none focus:ring-2 focus:ring-ring/20 focus:border-ring transition-colors",
                                                    CustomAttributes = { ["placeholder"] = "Search components...", ["type"] = "search" }
                                                }
                                            }
                                        }
                                    }
                                },
                                // Right side
                                new Box {
                                    ClassName = "flex items-center gap-2",
                                    Children = {
                                        // Dark/Light mode toggle
                                        new DynamicElement {
                                            TagName = "button",
                                            Id = "theme-toggle",
                                            ClassName = "inline-flex items-center justify-center rounded-lg h-9 w-9 text-muted-foreground hover:bg-accent hover:text-accent-foreground transition-colors",
                                            CustomAttributes = {
                                                ["onclick"] = "document.documentElement.classList.toggle('dark');localStorage.setItem('theme',document.documentElement.classList.contains('dark')?'dark':'light');document.getElementById('icon-sun').classList.toggle('hidden');document.getElementById('icon-moon').classList.toggle('hidden');",
                                                ["aria-label"] = "Toggle dark mode"
                                            },
                                            Children = {
                                                // Sun icon (visible in dark mode, click to go light)
                                                new Box {
                                                    Id = "icon-sun",
                                                    Children = { LucideIcons.Sun(size: 18, strokeWidth: 1.75) }
                                                },
                                                // Moon icon (visible in light mode, click to go dark)
                                                new Box {
                                                    Id = "icon-moon",
                                                    ClassName = "hidden",
                                                    Children = { LucideIcons.Moon(size: 18, strokeWidth: 1.75) }
                                                }
                                            }
                                        },
                                        new Button {
                                            Variant = Variant.Ghost,
                                            ClassName = "rounded-lg h-9 w-9 p-0",
                                            Children = { LucideIcons.Bell(size: 18, strokeWidth: 1.75) }
                                        },
                                        new Box {
                                            ClassName = "h-8 w-8 rounded-full bg-primary/10 border border-primary/20 flex items-center justify-center font-semibold text-xs text-primary",
                                            Children = { new Text("EM") }
                                        }
                                    }
                                }
                            }
                        },
                        // Main content
                        contentBox
                    }
                }
            }
        };
    }
}
