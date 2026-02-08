using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;
using eQuantic.UI.Components;
using eQuantic.UI.Components.Layout;
using TailwindDashboard.Components;

namespace TailwindDashboard.Pages.Components;

[Page("/components/layout", Title = "Layout")]
public class LayoutPage : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new DashboardShell
        {
            PageTitle = "Layout",
            ActivePath = "/components/layout",
            Children = {
                new Box {
                    ClassName = "space-y-12",
                    Children = {
                        // Grid
                        CreateSection("Grid", "CSS Grid layout with configurable columns and gap.",
                            new Box {
                                ClassName = "space-y-6",
                                Children = {
                                    new Text("3 Columns") { ClassName = "text-sm font-medium text-muted-foreground" },
                                    new Grid {
                                        Columns = 3,
                                        Gap = "1rem",
                                        Children = {
                                            CreateGridCell("1"),
                                            CreateGridCell("2"),
                                            CreateGridCell("3"),
                                            CreateGridCell("4"),
                                            CreateGridCell("5"),
                                            CreateGridCell("6")
                                        }
                                    },
                                    new Text("4 Columns") { ClassName = "text-sm font-medium text-muted-foreground" },
                                    new Grid {
                                        Columns = 4,
                                        Gap = "0.75rem",
                                        Children = {
                                            CreateGridCell("1"),
                                            CreateGridCell("2"),
                                            CreateGridCell("3"),
                                            CreateGridCell("4")
                                        }
                                    },
                                    new Text("2 Columns with ColSpan") { ClassName = "text-sm font-medium text-muted-foreground" },
                                    new Grid {
                                        Columns = 3,
                                        Gap = "1rem",
                                        Children = {
                                            new GridItem {
                                                ColSpan = 2,
                                                Children = { CreateGridCell("Span 2") }
                                            },
                                            CreateGridCell("1"),
                                            CreateGridCell("1"),
                                            new GridItem {
                                                ColSpan = 2,
                                                Children = { CreateGridCell("Span 2") }
                                            }
                                        }
                                    }
                                }
                            }
                        ),

                        // Flex
                        CreateSection("Flex", "Flexbox container with direction, alignment, and gap.",
                            new Box {
                                ClassName = "space-y-6",
                                Children = {
                                    new Text("Row (default)") { ClassName = "text-sm font-medium text-muted-foreground" },
                                    new Flex {
                                        Direction = FlexDirection.Row,
                                        Gap = "0.5rem",
                                        Children = {
                                            CreateFlexItem("1"),
                                            CreateFlexItem("2"),
                                            CreateFlexItem("3")
                                        }
                                    },
                                    new Text("Row Reverse") { ClassName = "text-sm font-medium text-muted-foreground" },
                                    new Flex {
                                        Direction = FlexDirection.RowReverse,
                                        Gap = "0.5rem",
                                        Children = {
                                            CreateFlexItem("1"),
                                            CreateFlexItem("2"),
                                            CreateFlexItem("3")
                                        }
                                    },
                                    new Text("Column") { ClassName = "text-sm font-medium text-muted-foreground" },
                                    new Flex {
                                        Direction = FlexDirection.Column,
                                        Gap = "0.5rem",
                                        Children = {
                                            CreateFlexItem("1"),
                                            CreateFlexItem("2"),
                                            CreateFlexItem("3")
                                        }
                                    },
                                    new Text("Justify Content") { ClassName = "text-sm font-medium text-muted-foreground" },
                                    new Box {
                                        ClassName = "space-y-2",
                                        Children = {
                                            CreateJustifyDemo("FlexStart", JustifyContent.FlexStart),
                                            CreateJustifyDemo("Center", JustifyContent.Center),
                                            CreateJustifyDemo("FlexEnd", JustifyContent.FlexEnd),
                                            CreateJustifyDemo("SpaceBetween", JustifyContent.SpaceBetween),
                                            CreateJustifyDemo("SpaceAround", JustifyContent.SpaceAround)
                                        }
                                    },
                                    new Text("Wrap") { ClassName = "text-sm font-medium text-muted-foreground" },
                                    new Flex {
                                        Wrap = true,
                                        Gap = "0.5rem",
                                        Children = {
                                            CreateFlexItem("A"),
                                            CreateFlexItem("B"),
                                            CreateFlexItem("C"),
                                            CreateFlexItem("D"),
                                            CreateFlexItem("E"),
                                            CreateFlexItem("F"),
                                            CreateFlexItem("G"),
                                            CreateFlexItem("H")
                                        }
                                    }
                                }
                            }
                        ),

                        // Stack / VStack
                        CreateSection("VStack", "Vertical stack with automatic gap between children.",
                            new VStack {
                                Children = {
                                    CreateFlexItem("Item 1"),
                                    CreateFlexItem("Item 2"),
                                    CreateFlexItem("Item 3")
                                }
                            }
                        ),

                        // HStack
                        CreateSection("HStack", "Horizontal stack with automatic gap and centered alignment.",
                            new HStack {
                                Children = {
                                    CreateFlexItem("Left"),
                                    CreateFlexItem("Center"),
                                    CreateFlexItem("Right")
                                }
                            }
                        ),

                        // Row / Column
                        CreateSection("Row & Column", "Semantic layout components for horizontal and vertical flow.",
                            new Box {
                                ClassName = "space-y-4",
                                Children = {
                                    new Text("Row") { ClassName = "text-sm font-medium text-muted-foreground" },
                                    new Row {
                                        Children = {
                                            CreateFlexItem("A"),
                                            CreateFlexItem("B"),
                                            CreateFlexItem("C")
                                        }
                                    },
                                    new Text("Column") { ClassName = "text-sm font-medium text-muted-foreground" },
                                    new Column {
                                        Children = {
                                            CreateFlexItem("X"),
                                            CreateFlexItem("Y"),
                                            CreateFlexItem("Z")
                                        }
                                    }
                                }
                            }
                        ),

                        // Container
                        CreateSection("Container", "Content container with max-width and centering.",
                            new Box {
                                ClassName = "space-y-4",
                                Children = {
                                    new Text("Default Container") { ClassName = "text-sm font-medium text-muted-foreground" },
                                    new Container {
                                        ClassName = "border border-dashed border-border p-4 rounded",
                                        Children = {
                                            new Text("This is inside a Container component.") { ClassName = "text-sm" }
                                        }
                                    },
                                    new Text("Fluid Container") { ClassName = "text-sm font-medium text-muted-foreground" },
                                    new Container {
                                        Fluid = true,
                                        ClassName = "border border-dashed border-border p-4 rounded",
                                        Children = {
                                            new Text("This is a fluid container that takes full width.") { ClassName = "text-sm" }
                                        }
                                    }
                                }
                            }
                        ),

                        // Box
                        CreateSection("Box", "Basic div wrapper for grouping and styling.",
                            new Box {
                                ClassName = "space-y-4",
                                Children = {
                                    new Box {
                                        ClassName = "p-4 border border-border rounded-lg bg-muted/50",
                                        Children = {
                                            new Text("A styled Box with padding, border, and background.") { ClassName = "text-sm" }
                                        }
                                    },
                                    new Box {
                                        ClassName = "p-4 border-2 border-dashed border-blue-300 rounded-lg",
                                        Children = {
                                            new Text("A Box with dashed blue border.") { ClassName = "text-sm text-blue-600 dark:text-blue-400" }
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

    private static IComponent CreateGridCell(string label)
    {
        return new Box {
            ClassName = "flex items-center justify-center p-4 rounded-lg border border-border bg-muted/50 text-sm font-medium",
            Children = { new Text(label) }
        };
    }

    private static IComponent CreateFlexItem(string label)
    {
        return new Box {
            ClassName = "flex items-center justify-center px-4 py-2 rounded-md border border-border bg-muted/50 text-sm font-medium min-w-[60px]",
            Children = { new Text(label) }
        };
    }

    private static IComponent CreateJustifyDemo(string label, JustifyContent justify)
    {
        return new Box {
            ClassName = "space-y-1",
            Children = {
                new Text(label) { ClassName = "text-xs text-muted-foreground" },
                new Flex {
                    Justify = justify,
                    Gap = "0.5rem",
                    ClassName = "p-2 border border-dashed border-border rounded",
                    Children = {
                        CreateFlexItem("A"),
                        CreateFlexItem("B"),
                        CreateFlexItem("C")
                    }
                }
            }
        };
    }

    private static IComponent CreateSection(string title, string description, IComponent content)
    {
        return new Box {
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
    }
}
