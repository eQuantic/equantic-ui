using eQuantic.UI.Core;
using eQuantic.UI.Web.Components;
using eQuantic.UI.Web.Components.Layout;
using eQuantic.UI.Web.Components.Surfaces;
using eQuantic.UI.Core.Theme.Types;
using eQuantic.UI.Material;

namespace MaterialDashboard.Pages;

[Page("/")]
public class HomePage : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new Box
        {
            ClassName = "eq-min-h-screen eq-surface-subtle eq-text-primary",
            Children = {
                // Top App Bar
                new Box {
                    ClassName = "eq-h-16 eq-flex eq-items-center eq-px-4 eq-gap-4 eq-surface eq-border-b eq-sticky eq-top-0 eq-z-50",
                    Children = {
                        new Button { Variant = Variant.Ghost, ClassName = "eq-p-2", Children = { new Text("menu") { ClassName = "material-icons" } } },
                        new Text("Material Dashboard") { ClassName = $"{M3.Typography.TitleLarge} eq-flex-1" },
                        new Button { Variant = Variant.Ghost, ClassName = "eq-p-2", Children = { new Text("search") { ClassName = "material-icons" } } },
                        new Button { Variant = Variant.Ghost, ClassName = "eq-p-2", Children = { new Text("account_circle") { ClassName = "material-icons" } } }
                    }
                },
                
                // Main Content
                new Box {
                    ClassName = "eq-p-6 eq-max-w-7xl eq-mx-auto eq-flex eq-gap-6",
                    Children = {
                        // Navigation Rail (Mock)
                        new Box {
                            ClassName = "eq-w-24 eq-flex eq-flex-col eq-items-center eq-gap-6 eq-pt-4",
                            Children = {
                                CreateRailItem("home", "Home", true),
                                CreateRailItem("dashboard", "Stats", false),
                                new Link { Href = "/showcase", Children = { CreateRailItem("widgets", "Showcase", false) } },
                                CreateRailItem("settings", "Settings", false)
                            }
                        },
                        
                        // Right Side
                        new Box {
                            ClassName = "flex-1 space-y-8",
                            Children = {
                                new Heading("Welcome back", 1) { ClassName = $"{M3.Typography.HeadlineLarge} eq-tracking-tight" },
                                
                                new Grid {
                                    Columns = 3,
                                    Gap = "1.5rem",
                                    Children = {
                                        new Card {
                                            Variant = CardVariant.Elevated,
                                            Children = {
                                                new Box {
                                                    ClassName = "p-6",
                                                    Children = {
                                                        new Text("Pending Tasks") { ClassName = $"{M3.Typography.LabelLarge} eq-mb-1 eq-block" },
                                                        new Text("24") { ClassName = M3.Typography.DisplaySmall }
                                                    }
                                                }
                                            }
                                        },
                                        new Card {
                                            Variant = CardVariant.Elevated,
                                            Children = {
                                                new Box {
                                                    ClassName = "p-6",
                                                    Children = {
                                                        new Text("New Messages") { ClassName = $"{M3.Typography.LabelLarge} eq-mb-1 eq-block" },
                                                        new Text("12") { ClassName = M3.Typography.DisplaySmall }
                                                    }
                                                }
                                            }
                                        },
                                        new Card {
                                            Variant = CardVariant.Elevated,
                                            Children = {
                                                new Box {
                                                    ClassName = "p-6",
                                                    Children = {
                                                        new Text("System Health") { ClassName = $"{M3.Typography.LabelLarge} eq-mb-1 eq-block" },
                                                        new Text("Optimal") { ClassName = $"{M3.Typography.DisplaySmall} eq-text-success" }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                                
                                new Box {
                                    ClassName = "eq-surface eq-rounded-3xl eq-p-8",
                                    Children = {
                                        new Heading("Recent Activity", 2) { ClassName = $"{M3.Typography.HeadlineSmall} eq-mb-6" },
                                        new Box {
                                            ClassName = "space-y-4",
                                            Children = {
                                                CreateActivityItem("check_circle", "Project Milestone reached", "2 hours ago"),
                                                CreateActivityItem("person_add", "New user registered", "5 hours ago"),
                                                CreateActivityItem("update", "System update completed", "Yesterday")
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    private IComponent CreateRailItem(string icon, string label, bool active)
    {
        return new Box {
            ClassName = "flex flex-col items-center gap-1 group cursor-pointer",
            Children = {
                new Box {
                    ClassName = $"w-14 h-8 rounded-full flex items-center justify-center transition-all {(active ? "bg-[#d3e3fd] text-[#041e49]" : "hover:bg-[#f2f2f2] text-[#44474e]")}",
                    Children = { new Text(icon) { ClassName = "material-icons" } }
                },
                new Text(label) { ClassName = $"text-xs font-medium {(active ? "text-[#041e49]" : "text-[#44474e]")}" }
            }
        };
    }

    private IComponent CreateActivityItem(string icon, string title, string subtitle)
    {
        return new Box {
            ClassName = "flex items-center gap-4 py-2",
            Children = {
                new Box {
                    ClassName = "eq-w-10 eq-h-10 eq-rounded-full eq-surface-subtle eq-flex eq-items-center eq-justify-center",
                    Children = { new Text(icon) { ClassName = "material-icons eq-text-secondary" } }
                },
                new Box {
                    ClassName = "flex-1",
                    Children = {
                        new Text(title) { ClassName = $"{M3.Typography.TitleMedium} eq-block" },
                        new Text(subtitle) { ClassName = $"{M3.Typography.BodySmall} eq-text-secondary" }
                    }
                }
            }
        };
    }
}
