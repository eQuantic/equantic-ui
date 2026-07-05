using System;
using System.Collections.Generic;
using eQuantic.UI.Core;
using eQuantic.UI.Web.Components;
using eQuantic.UI.Web.Components.Layout;
using eQuantic.UI.Web.Components.Overlays.Drawer;
using eQuantic.UI.Web.Components.Navigation.Tabs;
using eQuantic.UI.Web.Components.Display.Accordion;
using eQuantic.UI.Web.Components.Navigation.NavigationMenu;
using eQuantic.UI.Web.Components.Overlays.ContextMenu;
using DefaultUIDashboard.Components;

namespace DefaultUIDashboard.Pages;

[Page("/showcase")]
public class Showcase : StatefulComponent
{
    public override ComponentState CreateState() => new ShowcaseState();
}

public class ShowcaseState : ComponentState<Showcase>
{
    private bool _isDrawerOpen = false;
    private string _activeTab = "tabs";

    public override IComponent Build(RenderContext context)
    {
        return new DefaultDashboardShell
        {
            ActivePath = "/showcase",
            Children = {
                new Box {
                    ClassName = "max-w-4xl mx-auto",
                    Children = {
                        new Heading("UI Showcase", 1) { ClassName = "text-3xl font-bold mb-8" },

                        new Tabs {
                            Value = _activeTab,
                            OnValueChange = (val) => SetState(() => _activeTab = val),
                            Children = {
                                new TabsList {
                                    Children = {
                                        new TabsTrigger { Value = "tabs", Text = "Tabs" },
                                        new TabsTrigger { Value = "accordion", Text = "Accordion" },
                                        new TabsTrigger { Value = "drawer", Text = "Drawer" },
                                        new TabsTrigger { Value = "context", Text = "Context Menu" }
                                    }
                                },
                                new TabsContent {
                                    Value = "tabs",
                                    Children = {
                                        new Box { ClassName = "p-4 border border-slate-800 rounded-md mt-4 bg-slate-900", Children = { new Text("Tabs are working! This is the content for the first tab.") } }
                                    }
                                },
                                new TabsContent {
                                    Value = "accordion",
                                    Children = {
                                        new Accordion {
                                            Type = AccordionType.Single,
                                            Children = {
                                                new AccordionItem {
                                                    Value = "item-1",
                                                    Children = {
                                                        new AccordionTrigger { Text = "What is eQuantic.UI?" },
                                                        new AccordionContent { Children = { new Text("A modern UI framework for .NET with a focus on developer experience and premium aesthetics.") } }
                                                    }
                                                },
                                                new AccordionItem {
                                                    Value = "item-2",
                                                    Children = {
                                                        new AccordionTrigger { Text = "Is it composed?" },
                                                        new AccordionContent { Children = { new Text("Yes, it uses a composition-based architecture inspired by Radix UI and Shadcn UI.") } }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                                new TabsContent {
                                    Value = "drawer",
                                    Children = {
                                        new Box {
                                            ClassName = "p-8 border-dashed border-2 border-slate-800 rounded-lg text-center mt-4",
                                            Children = {
                                                new Button {
                                                    Text = "Open Drawer",
                                                    ClassName = "bg-indigo-600 px-6 py-2 rounded-lg",
                                                    OnClick = () => SetState(() => _isDrawerOpen = true)
                                                }
                                            }
                                        },
                                        new Drawer {
                                            IsOpen = _isDrawerOpen,
                                            OnClose = () => SetState(() => _isDrawerOpen = false),
                                            Children = {
                                                new DrawerContent {
                                                    Children = {
                                                        new DrawerHeader {
                                                            Children = {
                                                                new DrawerTitle { Text = "Edit Profile" },
                                                                new DrawerDescription { Text = "Make changes to your profile here. Click save when you're done." }
                                                            }
                                                        },
                                                        new Box { ClassName = "p-4 flex-1", Children = { new Text("This is the drawer content area.") } },
                                                        new DrawerFooter {
                                                            Children = {
                                                                new Button { Text = "Save changes", ClassName = "bg-indigo-600 px-4 py-2 rounded" },
                                                                new DrawerClose { Children = { new Button { Text = "Cancel", Variant = eQuantic.UI.Core.Theme.Types.Variant.Outline } } }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                                new TabsContent {
                                    Value = "context",
                                    Children = {
                                        new ContextMenu {
                                            Children = {
                                                new ContextMenuTrigger {
                                                    ClassName = "flex h-[150px] w-full items-center justify-center rounded-md border border-dashed border-slate-800 text-sm mt-4",
                                                    Children = { new Text("Right click here to open the menu") }
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
