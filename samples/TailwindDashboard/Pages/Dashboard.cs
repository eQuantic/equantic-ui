using eQuantic.UI.Core;
using eQuantic.UI.Components;
using eQuantic.UI.Components.Layout;
using TailwindDashboard.Components;

namespace TailwindDashboard.Pages;

[Page("/", Title = "Dashboard")]
public class Dashboard : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new DashboardShell
        {
            PageTitle = "Personal Dashboard",
            ActivePath = "/",
            Children = {
                new Grid {
                    Columns = 3,
                    Gap = "1rem",
                    Children = {
                        CreateStatCard("Tasks Completed", "12", "+2 since yesterday", "✅"),
                        CreateStatCard("Active Projects", "4", "On track", "📂"),
                        CreateStatCard("Productivity Score", "85%", "Top 10% this week", "📈")
                    }
                },
                new Box {
                    ClassName = "mt-8 grid gap-4 md:grid-cols-2 lg:grid-cols-7",
                    Children = {
                        new Box {
                            ClassName = "col-span-4 rounded-xl border border-border bg-card text-card-foreground shadow p-6",
                            Children = {
                                new Heading("Overview", 2) { ClassName = "text-lg font-semibold mb-4" },
                                new Box {
                                    ClassName = "h-[200px] flex items-center justify-center border-2 border-dashed border-border rounded-lg text-muted-foreground",
                                    Children = { new Text("Chart placeholder") }
                                }
                            }
                        },
                        new Box {
                            ClassName = "col-span-3 rounded-xl border border-border bg-card text-card-foreground shadow p-6",
                            Children = {
                                new Heading("Recent Activity", 2) { ClassName = "text-lg font-semibold mb-4" },
                                new Box {
                                    ClassName = "space-y-4",
                                    Children = {
                                        CreateActivityItem("Completed 'Refactor Drawer'", "2 hours ago"),
                                        CreateActivityItem("New task 'Implement NavMenu'", "5 hours ago"),
                                        CreateActivityItem("Updated profile picture", "Yesterday")
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    private IComponent CreateStatCard(string title, string value, string footer, string icon)
    {
        return new Box {
            ClassName = "rounded-xl border border-border bg-card text-card-foreground p-6 shadow-sm",
            Children = {
                new Box {
                    ClassName = "flex items-center justify-between space-y-0 pb-2",
                    Children = {
                        new Text(title) { ClassName = "text-sm font-medium text-muted-foreground" },
                        new Text(icon)
                    }
                },
                new Box {
                    Children = {
                        new Text(value) { ClassName = "text-2xl font-bold" },
                        new Text(footer) { ClassName = "text-xs text-muted-foreground mt-1" }
                    }
                }
            }
        };
    }

    private IComponent CreateActivityItem(string title, string time)
    {
        return new Box {
            ClassName = "flex items-center",
            Children = {
                new Box { ClassName = "h-2 w-2 rounded-full bg-blue-500 mr-3" },
                new Box {
                    ClassName = "flex-1",
                    Children = {
                        new Text(title) { ClassName = "text-sm font-medium" },
                        new Text(time) { ClassName = "text-xs text-muted-foreground" }
                    }
                }
            }
        };
    }
}
