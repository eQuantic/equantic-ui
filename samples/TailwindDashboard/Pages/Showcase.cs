using eQuantic.UI.Core;
using eQuantic.UI.Components;
using eQuantic.UI.Components.Layout;
using eQuantic.UI.Components.Display.Accordion;
using eQuantic.UI.Components.Navigation.Tabs;
using eQuantic.UI.Components.Overlays.Drawer;
using eQuantic.UI.Components.Navigation.NavigationMenu;
using eQuantic.UI.Components.Overlays.ContextMenu;
using eQuantic.UI.Core.Theme.Types;
using TailwindDashboard.Components;

namespace TailwindDashboard.Pages;

[Page("/showcase", Title = "Component Showcase")]
public class Showcase : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new DashboardShell
        {
            PageTitle = "Component Showcase",
            ActivePath = "/showcase",
            Children = {
                new Box {
                    ClassName = "space-y-12",
                    Children = {
                        // Navigation Menu
                        CreateSection("Navigation Menu", new NavigationMenu {
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
                                                        new NavigationMenuLink { Href = "/", Children = { new Text("Introduction") } },
                                                        new NavigationMenuLink { Href = "/tasks", Children = { new Text("Installation") } }
                                                    }
                                                }
                                            }
                                        },
                                        new NavigationMenuItem {
                                            Children = {
                                                new NavigationMenuTrigger { Text = "Components" },
                                                new NavigationMenuContent {
                                                    ClassName = "grid w-[400px] gap-3 p-4",
                                                    Children = {
                                                        new NavigationMenuLink { Href = "#", Children = { new Text("Accordion") } },
                                                        new NavigationMenuLink { Href = "#", Children = { new Text("Tabs") } }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }),

                        // Tabs
                        CreateSection("Tabs", new Tabs {
                            DefaultValue = "account",
                            ClassName = "w-[400px]",
                            Children = {
                                new TabsList {
                                    Children = {
                                        new TabsTrigger { Value = "account", Text = "Account" },
                                        new TabsTrigger { Value = "password", Text = "Password" }
                                    }
                                },
                                new TabsContent { Value = "account", Children = { new Text("Make changes to your account here.") } },
                                new TabsContent { Value = "password", Children = { new Text("Change your password here.") } }
                            }
                        }),

                        // Accordion
                        CreateSection("Accordion", new Accordion {
                            Type = AccordionType.Single,
                            Collapsible = true,
                            ClassName = "w-full max-w-md",
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
                                        new AccordionContent { Children = { new Text("Yes. It comes with default styles that matches the other components' aesthetic.") } }
                                    }
                                }
                            }
                        }),

                        // Context Menu
                        CreateSection("Context Menu", new Box {
                            ClassName = "flex h-[150px] w-[300px] items-center justify-center rounded-md border border-dashed text-sm",
                            Children = {
                                new ContextMenuTrigger {
                                    Children = { new Text("Right click here") }
                                },
                                new ContextMenuContent {
                                    Children = {
                                        new ContextMenuItem { Children = { new Text("Back") }, Shortcut = "⌘[" },
                                        new ContextMenuItem { Children = { new Text("Forward") }, Shortcut = "⌘]", Disabled = true },
                                        new ContextMenuSeparator(),
                                        new ContextMenuItem { Children = { new Text("Reload") }, Shortcut = "⌘R" }
                                    }
                                }
                            }
                        }
                        ),

                        // Drawer
                        CreateSection("Drawer", new Box {
                            Children = {
                                new Drawer {
                                    Side = DrawerSide.Bottom,
                                    Children = {
                                        new DrawerTrigger { 
                                            Children = { new Button { Text = "Open Drawer", Variant = Variant.Outline } }
                                        },
                                        new DrawerContent {
                                            Children = {
                                                new DrawerHeader {
                                                    Children = {
                                                        new DrawerTitle { Text = "Are you absolutely sure?" },
                                                        new DrawerDescription { Text = "This action cannot be undone." }
                                                    }
                                                },
                                                new DrawerFooter {
                                                    Children = {
                                                        new Button { Text = "Submit", Variant = Variant.Primary },
                                                        new DrawerClose { Children = { new Button { Text = "Cancel", Variant = Variant.Ghost } } }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        })
                    }
                }
            }
        };
    }

    private IComponent CreateSection(string title, IComponent component)
    {
        return new Box {
            ClassName = "space-y-4",
            Children = {
                new Heading(title, 2) { ClassName = "text-xl font-semibold border-b pb-2" },
                new Box {
                    ClassName = "p-6 rounded-xl border border-border bg-muted/50 flex justify-center",
                    Children = { component }
                }
            }
        };
    }
}
