using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;
using eQuantic.UI.Components;
using eQuantic.UI.Components.Layout;
using eQuantic.UI.Components.Display;
using eQuantic.UI.Components.Surfaces;
using TailwindDashboard.Components;

namespace TailwindDashboard.Pages.Components;

[Page("/components/surfaces", Title = "Surfaces")]
public class SurfacesPage : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new DashboardShell
        {
            PageTitle = "Surfaces",
            ActivePath = "/components/surfaces",
            Children = {
                new Box {
                    ClassName = "space-y-12",
                    Children = {
                        // Card Variants
                        CreateSection("Card Variants", "Cards with different visual styles.",
                            new Grid {
                                Columns = 3,
                                Gap = "1rem",
                                Children = {
                                    CreateDemoCard("Default", "The default card style with standard appearance.", CardVariant.Default),
                                    CreateDemoCard("Outline", "A card with transparent background and visible border.", CardVariant.Outline),
                                    CreateDemoCard("Elevated", "A card with enhanced shadow for emphasis.", CardVariant.Elevated),
                                    CreateDemoCard("Subtle", "A card with muted background tint.", CardVariant.Subtle),
                                    CreateDemoCard("Ghost", "A card with no border or shadow.", CardVariant.Ghost)
                                }
                            },
                            """
                            new Card {
                                Variant = CardVariant.Default,
                                Children = {
                                    new CardHeader { Children = { new CardTitle { Text = "Title" } } },
                                    new CardBody { Children = { new Text("Content") } }
                                }
                            }
                            """
                        ),

                        // Card Shadows
                        CreateSection("Card Shadows", "Cards with different shadow levels.",
                            new Grid {
                                Columns = 3,
                                Gap = "1rem",
                                Children = {
                                    CreateShadowCard("None", Shadow.None),
                                    CreateShadowCard("Small", Shadow.Small),
                                    CreateShadowCard("Medium", Shadow.Medium),
                                    CreateShadowCard("Large", Shadow.Large),
                                    CreateShadowCard("XLarge", Shadow.XLarge)
                                }
                            },
                            """
                            new Card { Shadow = Shadow.Small }
                            new Card { Shadow = Shadow.Large }
                            """
                        ),

                        // Compound Components Pattern
                        CreateSection("Compound Components", "Card built with sub-components for maximum flexibility.",
                            new Box {
                                ClassName = "max-w-md",
                                Children = {
                                    new Card {
                                        Variant = CardVariant.Default,
                                        Children = {
                                            new CardHeader {
                                                Children = {
                                                    new CardTitle { Text = "Create Project" },
                                                    new CardDescription { Text = "Deploy your new project in one-click." }
                                                }
                                            },
                                            new CardBody {
                                                Children = {
                                                    new Box {
                                                        ClassName = "space-y-4",
                                                        Children = {
                                                            new Box {
                                                                Children = {
                                                                    new Text("Name") { ClassName = "text-sm font-medium mb-1 block" },
                                                                    new DynamicElement {
                                                                        TagName = "input",
                                                                        ClassName = "w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-sm placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring",
                                                                        CustomAttributes = { ["placeholder"] = "Name of your project" }
                                                                    }
                                                                }
                                                            },
                                                            new Box {
                                                                Children = {
                                                                    new Text("Framework") { ClassName = "text-sm font-medium mb-1 block" },
                                                                    new DynamicElement {
                                                                        TagName = "select",
                                                                        ClassName = "w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-1 focus:ring-ring",
                                                                        Children = {
                                                                            new DynamicElement { TagName = "option", Children = { new Text("Next.js") } },
                                                                            new DynamicElement { TagName = "option", Children = { new Text("SvelteKit") } },
                                                                            new DynamicElement { TagName = "option", Children = { new Text("Nuxt.js") } },
                                                                            new DynamicElement { TagName = "option", Children = { new Text("Remix") } }
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            },
                                            new CardFooter {
                                                Children = {
                                                    new Flex {
                                                        Justify = JustifyContent.SpaceBetween,
                                                        Children = {
                                                            new Button { Text = "Cancel", Variant = Variant.Ghost },
                                                            new Button { Text = "Deploy", Variant = Variant.Primary }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            },
                            """
                            new Card {
                                Variant = CardVariant.Default,
                                Children = {
                                    new CardHeader {
                                        Children = {
                                            new CardTitle { Text = "Create Project" },
                                            new CardDescription { Text = "Deploy your new project." }
                                        }
                                    },
                                    new CardBody { Children = { new Text("Body content") } },
                                    new CardFooter {
                                        Children = {
                                            new Button { Text = "Cancel", Variant = Variant.Ghost },
                                            new Button { Text = "Deploy", Variant = Variant.Primary }
                                        }
                                    }
                                }
                            }
                            """
                        ),

                        // Cards with different widths
                        CreateSection("Card Widths", "Cards with custom width configurations.",
                            new Flex {
                                Direction = FlexDirection.Column,
                                Gap = "1rem",
                                Children = {
                                    new Card {
                                        Width = "w-64",
                                        Variant = CardVariant.Outline,
                                        Children = {
                                            new CardHeader {
                                                Children = { new CardTitle { Text = "Small Card (w-64)" } }
                                            },
                                            new CardBody {
                                                Children = { new Text("Compact card for widgets.") }
                                            }
                                        }
                                    },
                                    new Card {
                                        Width = "w-96",
                                        Variant = CardVariant.Outline,
                                        Children = {
                                            new CardHeader {
                                                Children = { new CardTitle { Text = "Medium Card (w-96)" } }
                                            },
                                            new CardBody {
                                                Children = { new Text("Standard card for forms and content.") }
                                            }
                                        }
                                    },
                                    new Card {
                                        Width = "w-full max-w-2xl",
                                        Variant = CardVariant.Outline,
                                        Children = {
                                            new CardHeader {
                                                Children = { new CardTitle { Text = "Full Width Card (max-w-2xl)" } }
                                            },
                                            new CardBody {
                                                Children = { new Text("Wide card for detailed content sections.") }
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

    private static IComponent CreateDemoCard(string title, string description, CardVariant variant)
    {
        return new Card {
            Variant = variant,
            Children = {
                new CardHeader {
                    Children = {
                        new CardTitle { Text = title },
                        new CardDescription { Text = description }
                    }
                },
                new CardBody {
                    Children = {
                        new Text("Card body content goes here.")
                    }
                }
            }
        };
    }

    private static IComponent CreateShadowCard(string label, Shadow shadow)
    {
        return new Card {
            Shadow = shadow,
            Children = {
                new CardHeader {
                    Children = { new CardTitle { Text = $"Shadow: {label}" } }
                },
                new CardBody {
                    Children = { new Text($"This card uses Shadow.{label}.") }
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
