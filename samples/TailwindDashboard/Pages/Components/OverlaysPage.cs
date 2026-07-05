using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;
using eQuantic.UI.Web.Components;
using eQuantic.UI.Web.Components.Layout;
using eQuantic.UI.Web.Components.Display;
using eQuantic.UI.Web.Components.Overlays;
using eQuantic.UI.Web.Components.Overlays.Drawer;
using eQuantic.UI.Web.Components.Overlays.ContextMenu;
using TailwindDashboard.Components;

namespace TailwindDashboard.Pages.Components;

[Page("/components/overlays", Title = "Overlays")]
public class OverlaysPage : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new DashboardShell
        {
            PageTitle = "Overlays",
            ActivePath = "/components/overlays",
            Children = {
                new Box {
                    ClassName = "space-y-12",
                    Children = {
                        // Drawer - Bottom
                        CreateSection("Drawer - Bottom", "Slide-out panel from the bottom edge.",
                            new Drawer {
                                Side = DrawerSide.Bottom,
                                Children = {
                                    new DrawerTrigger {
                                        Children = { new Button { Text = "Open Bottom Drawer", Variant = Variant.Outline } }
                                    },
                                    new DrawerContent {
                                        Children = {
                                            new DrawerHeader {
                                                Children = {
                                                    new DrawerTitle { Text = "Edit Profile" },
                                                    new DrawerDescription { Text = "Make changes to your profile here. Click save when you're done." }
                                                }
                                            },
                                            new Box {
                                                ClassName = "p-4 space-y-4",
                                                Children = {
                                                    new Box {
                                                        Children = {
                                                            new Text("Name") { ClassName = "text-sm font-medium mb-1 block" },
                                                            new DynamicElement {
                                                                TagName = "input",
                                                                ClassName = "w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm",
                                                                CustomAttributes = { ["placeholder"] = "John Doe", ["value"] = "John Doe" }
                                                            }
                                                        }
                                                    },
                                                    new Box {
                                                        Children = {
                                                            new Text("Email") { ClassName = "text-sm font-medium mb-1 block" },
                                                            new DynamicElement {
                                                                TagName = "input",
                                                                ClassName = "w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm",
                                                                CustomAttributes = { ["placeholder"] = "john@example.com" }
                                                            }
                                                        }
                                                    }
                                                }
                                            },
                                            new DrawerFooter {
                                                Children = {
                                                    new Button { Text = "Save changes", Variant = Variant.Primary },
                                                    new DrawerClose { Children = { new Button { Text = "Cancel", Variant = Variant.Outline } } }
                                                }
                                            }
                                        }
                                    }
                                }
                            },
                            """
                            new Drawer {
                                Side = DrawerSide.Bottom,
                                Children = {
                                    new DrawerTrigger {
                                        Children = { new Button { Text = "Open", Variant = Variant.Outline } }
                                    },
                                    new DrawerContent {
                                        Children = {
                                            new DrawerHeader {
                                                Children = {
                                                    new DrawerTitle { Text = "Title" },
                                                    new DrawerDescription { Text = "Description" }
                                                }
                                            },
                                            new DrawerFooter {
                                                Children = {
                                                    new Button { Text = "Save", Variant = Variant.Primary },
                                                    new DrawerClose { Children = { new Button { Text = "Cancel" } } }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            """
                        ),

                        // Drawer - Right
                        CreateSection("Drawer - Right", "Slide-out panel from the right edge.",
                            new Drawer {
                                Side = DrawerSide.Right,
                                Children = {
                                    new DrawerTrigger {
                                        Children = { new Button { Text = "Open Right Drawer", Variant = Variant.Outline } }
                                    },
                                    new DrawerContent {
                                        Width = "w-[400px]",
                                        Children = {
                                            new DrawerHeader {
                                                Children = {
                                                    new DrawerTitle { Text = "Settings" },
                                                    new DrawerDescription { Text = "Adjust your application settings." }
                                                }
                                            },
                                            new Box {
                                                ClassName = "p-4",
                                                Children = {
                                                    new Text("Settings content goes here.") { ClassName = "text-sm text-muted-foreground" }
                                                }
                                            },
                                            new DrawerFooter {
                                                Children = {
                                                    new DrawerClose { Children = { new Button { Text = "Close", Variant = Variant.Ghost } } }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        ),

                        // Drawer - Left
                        CreateSection("Drawer - Left", "Slide-out panel from the left edge.",
                            new Drawer {
                                Side = DrawerSide.Left,
                                Children = {
                                    new DrawerTrigger {
                                        Children = { new Button { Text = "Open Left Drawer", Variant = Variant.Outline } }
                                    },
                                    new DrawerContent {
                                        Width = "w-[300px]",
                                        Children = {
                                            new DrawerHeader {
                                                Children = {
                                                    new DrawerTitle { Text = "Navigation" },
                                                    new DrawerDescription { Text = "Browse through sections." }
                                                }
                                            },
                                            new Box {
                                                ClassName = "p-4",
                                                Children = {
                                                    new Text("Navigation items go here.") { ClassName = "text-sm text-muted-foreground" }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        ),

                        // Drawer - Top
                        CreateSection("Drawer - Top", "Slide-out panel from the top edge.",
                            new Drawer {
                                Side = DrawerSide.Top,
                                Children = {
                                    new DrawerTrigger {
                                        Children = { new Button { Text = "Open Top Drawer", Variant = Variant.Outline } }
                                    },
                                    new DrawerContent {
                                        Children = {
                                            new DrawerHeader {
                                                Children = {
                                                    new DrawerTitle { Text = "Announcement" },
                                                    new DrawerDescription { Text = "Important notice for all users." }
                                                }
                                            },
                                            new Box {
                                                ClassName = "p-4",
                                                Children = {
                                                    new Text("This is a top drawer, great for announcements and banners.") { ClassName = "text-sm text-muted-foreground" }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        ),

                        // Context Menu
                        CreateSection("Context Menu", "Right-click context menu with keyboard shortcuts.",
                            new Box {
                                ClassName = "flex h-[200px] w-[400px] items-center justify-center rounded-md border border-dashed border-border text-sm",
                                Children = {
                                    new ContextMenuTrigger {
                                        Children = { new Text("Right click here") { ClassName = "text-muted-foreground" } }
                                    },
                                    new ContextMenuContent {
                                        Children = {
                                            new ContextMenuItem { Children = { new Text("Back") }, Shortcut = "\u2318[" },
                                            new ContextMenuItem { Children = { new Text("Forward") }, Shortcut = "\u2318]", Disabled = true },
                                            new ContextMenuItem { Children = { new Text("Reload") }, Shortcut = "\u2318R" },
                                            new ContextMenuSeparator(),
                                            new ContextMenuItem { Children = { new Text("Save As...") }, Shortcut = "\u21e7\u2318S" },
                                            new ContextMenuItem { Children = { new Text("Print") }, Shortcut = "\u2318P" },
                                            new ContextMenuSeparator(),
                                            new ContextMenuItem { Children = { new Text("View Source") } },
                                            new ContextMenuItem { Children = { new Text("Inspect Element") } }
                                        }
                                    }
                                }
                            },
                            """
                            new ContextMenuTrigger { Children = { new Text("Right click here") } }
                            new ContextMenuContent {
                                Children = {
                                    new ContextMenuItem { Children = { new Text("Copy") }, Shortcut = "⌘C" },
                                    new ContextMenuSeparator(),
                                    new ContextMenuItem { Children = { new Text("Delete") } }
                                }
                            }
                            """
                        ),

                        // Modal (static preview since it requires state)
                        CreateSection("Modal", "Dialog overlay (static preview - requires StatefulComponent for interactive use).",
                            new Box {
                                ClassName = "space-y-4",
                                Children = {
                                    new Text("Modals require state management to toggle IsOpen. Below is a structural preview.") { ClassName = "text-sm text-muted-foreground" },
                                    new Box {
                                        ClassName = "relative p-6 rounded-lg border border-border bg-card shadow-lg max-w-md",
                                        Children = {
                                            new Box {
                                                ClassName = "space-y-2",
                                                Children = {
                                                    new Heading("Are you sure?", 3) { ClassName = "text-lg font-semibold" },
                                                    new Text("This action cannot be undone. This will permanently delete your account and remove your data from our servers.") { ClassName = "text-sm text-muted-foreground" }
                                                }
                                            },
                                            new Flex {
                                                Justify = JustifyContent.FlexEnd,
                                                Gap = "0.5rem",
                                                ClassName = "mt-6",
                                                Children = {
                                                    new Button { Text = "Cancel", Variant = Variant.Outline },
                                                    new Button { Text = "Continue", Variant = Variant.Destructive }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
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
