using System.Collections.Generic;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;
using eQuantic.UI.Components;
using eQuantic.UI.Components.Layout;
using eQuantic.UI.Components.Navigation;
using eQuantic.UI.Components.Navigation.Tabs;
using eQuantic.UI.Components.Display;
using eQuantic.UI.Components.Display.Accordion;
using eQuantic.UI.Components.Navigation.NavigationMenu;
using TailwindDashboard.Components;

namespace TailwindDashboard.Pages.Components;

[Page("/components/navigation", Title = "Navigation")]
public class NavigationPage : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new DashboardShell
        {
            PageTitle = "Navigation",
            ActivePath = "/components/navigation",
            Children = {
                new Box {
                    ClassName = "space-y-12",
                    Children = {
                        // Tabs - Horizontal
                        CreateSection("Tabs - Horizontal", "Standard horizontal tab navigation.",
                            new Tabs {
                                DefaultValue = "account",
                                ClassName = "w-full max-w-lg",
                                Children = {
                                    new TabsList {
                                        Children = {
                                            new TabsTrigger { Value = "account", Text = "Account" },
                                            new TabsTrigger { Value = "password", Text = "Password" },
                                            new TabsTrigger { Value = "settings", Text = "Settings" }
                                        }
                                    },
                                    new TabsContent {
                                        Value = "account",
                                        Children = {
                                            new Box {
                                                ClassName = "space-y-2 pt-4",
                                                Children = {
                                                    new Heading("Account", 3) { ClassName = "text-lg font-semibold" },
                                                    new Text("Make changes to your account here. Click save when you're done.") { ClassName = "text-sm text-muted-foreground" }
                                                }
                                            }
                                        }
                                    },
                                    new TabsContent {
                                        Value = "password",
                                        Children = {
                                            new Box {
                                                ClassName = "space-y-2 pt-4",
                                                Children = {
                                                    new Heading("Password", 3) { ClassName = "text-lg font-semibold" },
                                                    new Text("Change your password here. After saving, you'll be logged out.") { ClassName = "text-sm text-muted-foreground" }
                                                }
                                            }
                                        }
                                    },
                                    new TabsContent {
                                        Value = "settings",
                                        Children = {
                                            new Box {
                                                ClassName = "space-y-2 pt-4",
                                                Children = {
                                                    new Heading("Settings", 3) { ClassName = "text-lg font-semibold" },
                                                    new Text("Manage your application preferences and configuration.") { ClassName = "text-sm text-muted-foreground" }
                                                }
                                            }
                                        }
                                    }
                                }
                            },
                            """
                            new Tabs {
                                DefaultValue = "account",
                                Children = {
                                    new TabsList {
                                        Children = {
                                            new TabsTrigger { Value = "account", Text = "Account" },
                                            new TabsTrigger { Value = "password", Text = "Password" }
                                        }
                                    },
                                    new TabsContent {
                                        Value = "account",
                                        Children = { new Text("Account settings.") }
                                    },
                                    new TabsContent {
                                        Value = "password",
                                        Children = { new Text("Password settings.") }
                                    }
                                }
                            }
                            """
                        ),

                        // Tabs - Vertical
                        CreateSection("Tabs - Vertical", "Vertical tab layout for sidebar-style navigation.",
                            new Tabs {
                                DefaultValue = "general",
                                Vertical = true,
                                ClassName = "w-full max-w-lg",
                                Children = {
                                    new TabsList {
                                        Children = {
                                            new TabsTrigger { Value = "general", Text = "General" },
                                            new TabsTrigger { Value = "appearance", Text = "Appearance" },
                                            new TabsTrigger { Value = "notifications", Text = "Notifications" },
                                            new TabsTrigger { Value = "display", Text = "Display" }
                                        }
                                    },
                                    new TabsContent {
                                        Value = "general",
                                        Children = { new Text("General settings content.") { ClassName = "text-sm" } }
                                    },
                                    new TabsContent {
                                        Value = "appearance",
                                        Children = { new Text("Appearance customization options.") { ClassName = "text-sm" } }
                                    },
                                    new TabsContent {
                                        Value = "notifications",
                                        Children = { new Text("Notification preferences.") { ClassName = "text-sm" } }
                                    },
                                    new TabsContent {
                                        Value = "display",
                                        Children = { new Text("Display and layout settings.") { ClassName = "text-sm" } }
                                    }
                                }
                            }
                        ),

                        // Tabs - Disabled
                        CreateSection("Tabs - With Disabled Tab", "Tab with a disabled trigger.",
                            new Tabs {
                                DefaultValue = "active",
                                ClassName = "w-full max-w-md",
                                Children = {
                                    new TabsList {
                                        Children = {
                                            new TabsTrigger { Value = "active", Text = "Active" },
                                            new TabsTrigger { Value = "disabled", Text = "Disabled", Disabled = true },
                                            new TabsTrigger { Value = "another", Text = "Another" }
                                        }
                                    },
                                    new TabsContent { Value = "active", Children = { new Text("This tab is active.") } },
                                    new TabsContent { Value = "another", Children = { new Text("Another tab content.") } }
                                }
                            }
                        ),

                        // Accordion - Single
                        CreateSection("Accordion - Single", "Only one item can be expanded at a time.",
                            new Accordion {
                                Type = AccordionType.Single,
                                Collapsible = true,
                                ClassName = "w-full max-w-lg",
                                Children = {
                                    new AccordionItem {
                                        Value = "item-1",
                                        Children = {
                                            new AccordionTrigger { Text = "Is it accessible?" },
                                            new AccordionContent { Children = { new Text("Yes. It adheres to the WAI-ARIA design pattern.") } }
                                        }
                                    },
                                    new AccordionItem {
                                        Value = "item-2",
                                        Children = {
                                            new AccordionTrigger { Text = "Is it styled?" },
                                            new AccordionContent { Children = { new Text("Yes. It comes with default styles that match the other components.") } }
                                        }
                                    },
                                    new AccordionItem {
                                        Value = "item-3",
                                        Children = {
                                            new AccordionTrigger { Text = "Is it animated?" },
                                            new AccordionContent { Children = { new Text("Yes. It's animated by default, but you can customize transitions.") } }
                                        }
                                    }
                                }
                            },
                            """
                            new Accordion {
                                Type = AccordionType.Single,
                                Collapsible = true,
                                Children = {
                                    new AccordionItem {
                                        Value = "item-1",
                                        Children = {
                                            new AccordionTrigger { Text = "Question?" },
                                            new AccordionContent { Children = { new Text("Answer.") } }
                                        }
                                    }
                                }
                            }
                            """
                        ),

                        // Accordion - Multiple
                        CreateSection("Accordion - Multiple", "Multiple items can be expanded simultaneously.",
                            new Accordion {
                                Type = AccordionType.Multiple,
                                ClassName = "w-full max-w-lg",
                                Children = {
                                    new AccordionItem {
                                        Value = "multi-1",
                                        Children = {
                                            new AccordionTrigger { Text = "Section One" },
                                            new AccordionContent { Children = { new Text("Content for section one. This can be expanded alongside others.") } }
                                        }
                                    },
                                    new AccordionItem {
                                        Value = "multi-2",
                                        Children = {
                                            new AccordionTrigger { Text = "Section Two" },
                                            new AccordionContent { Children = { new Text("Content for section two. Try expanding multiple sections.") } }
                                        }
                                    },
                                    new AccordionItem {
                                        Value = "multi-3",
                                        Children = {
                                            new AccordionTrigger { Text = "Section Three" },
                                            new AccordionContent { Children = { new Text("Content for section three.") } }
                                        }
                                    }
                                }
                            }
                        ),

                        // Navigation Menu
                        CreateSection("Navigation Menu", "Dropdown navigation with nested content.",
                            new NavigationMenu {
                                ClassName = "border border-border rounded-lg p-2 bg-card",
                                Children = {
                                    new NavigationMenuList {
                                        Children = {
                                            new NavigationMenuItem {
                                                Children = {
                                                    new NavigationMenuTrigger { Text = "Getting Started" },
                                                    new NavigationMenuContent {
                                                        ClassName = "grid gap-3 p-4 md:w-[400px] lg:w-[500px]",
                                                        Children = {
                                                            new NavigationMenuLink { Href = "#", Children = { new Text("Introduction - A brief overview of the framework.") } },
                                                            new NavigationMenuLink { Href = "#", Children = { new Text("Installation - How to install and set up the project.") } },
                                                            new NavigationMenuLink { Href = "#", Children = { new Text("Quick Start - Build your first component.") } }
                                                        }
                                                    }
                                                }
                                            },
                                            new NavigationMenuItem {
                                                Children = {
                                                    new NavigationMenuTrigger { Text = "Components" },
                                                    new NavigationMenuContent {
                                                        ClassName = "grid w-[400px] gap-3 p-4 md:grid-cols-2",
                                                        Children = {
                                                            new NavigationMenuLink { Href = "/components/buttons", Children = { new Text("Buttons") } },
                                                            new NavigationMenuLink { Href = "/components/inputs", Children = { new Text("Inputs") } },
                                                            new NavigationMenuLink { Href = "/components/display", Children = { new Text("Display") } },
                                                            new NavigationMenuLink { Href = "/components/surfaces", Children = { new Text("Surfaces") } }
                                                        }
                                                    }
                                                }
                                            },
                                            new NavigationMenuItem {
                                                Children = {
                                                    new NavigationMenuLink { Href = "#", Children = { new Text("Documentation") } }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        ),

                        // Breadcrumb
                        CreateSection("Breadcrumb", "Hierarchical navigation trail.",
                            new Box {
                                ClassName = "space-y-6",
                                Children = {
                                    new Text("Standard Breadcrumb") { ClassName = "text-sm font-medium text-muted-foreground" },
                                    new Breadcrumb {
                                        Items = new List<BreadcrumbItem> {
                                            new BreadcrumbItem { Label = "Home", Href = "/" },
                                            new BreadcrumbItem { Label = "Components", Href = "/components/buttons" },
                                            new BreadcrumbItem { Label = "Navigation", Active = true }
                                        }
                                    },
                                    new Text("Custom Separator") { ClassName = "text-sm font-medium text-muted-foreground" },
                                    new Breadcrumb {
                                        Separator = ">",
                                        Items = new List<BreadcrumbItem> {
                                            new BreadcrumbItem { Label = "Dashboard", Href = "/" },
                                            new BreadcrumbItem { Label = "Settings", Href = "#" },
                                            new BreadcrumbItem { Label = "Profile", Active = true }
                                        }
                                    }
                                }
                            },
                            """
                            new Breadcrumb {
                                Items = new List<BreadcrumbItem> {
                                    new BreadcrumbItem { Label = "Home", Href = "/" },
                                    new BreadcrumbItem { Label = "Components", Href = "/components" },
                                    new BreadcrumbItem { Label = "Current", Active = true }
                                }
                            }
                            """
                        )
                    }
                }
            }
        };
    }

    private static IComponent CreateSection(string title, string description, IComponent content, string? code = null)
    {
        var section = new Box {
            ClassName = "space-y-3",
            Children = {
                new Box {
                    ClassName = "space-y-1",
                    Children = {
                        new Heading(title, 3) { ClassName = "text-lg font-semibold tracking-tight" },
                        new Text(description) { ClassName = "text-[13px] text-muted-foreground leading-relaxed" }
                    }
                },
                new Box {
                    ClassName = "rounded-xl border border-border/60 bg-card p-6 shadow-sm",
                    Children = { content }
                }
            }
        };

        if (code != null)
        {
            section.Children.Add(new CodeBlock(code.Trim(), "csharp") { Collapsible = true });
        }

        return section;
    }
}
