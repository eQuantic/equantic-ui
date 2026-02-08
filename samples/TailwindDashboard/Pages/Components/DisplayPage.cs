using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;
using eQuantic.UI.Components;
using eQuantic.UI.Components.Layout;
using eQuantic.UI.Components.Display;
using eQuantic.UI.Components.Feedback;
using TailwindDashboard.Components;

namespace TailwindDashboard.Pages.Components;

[Page("/components/display", Title = "Display")]
public class DisplayPage : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new DashboardShell
        {
            PageTitle = "Display",
            ActivePath = "/components/display",
            Children = {
                new Box {
                    ClassName = "space-y-12",
                    Children = {
                        // Badge variants
                        CreateSection("Badge - Variants", "Inline status indicators with semantic colors.",
                            new Flex {
                                Wrap = true, Gap = "0.5rem",
                                Align = AlignItem.Center,
                                Children = {
                                    new Badge("Default") { Variant = Variant.Default },
                                    new Badge("Primary") { Variant = Variant.Primary },
                                    new Badge("Secondary") { Variant = Variant.Secondary },
                                    new Badge("Destructive") { Variant = Variant.Destructive },
                                    new Badge("Outline") { Variant = Variant.Outline },
                                    new Badge("Success") { Variant = Variant.Success },
                                    new Badge("Warning") { Variant = Variant.Warning },
                                    new Badge("Info") { Variant = Variant.Info }
                                }
                            }
                        ),

                        // Avatar
                        CreateSection("Avatar", "User avatars with image or initials fallback.",
                            new Box {
                                ClassName = "space-y-6",
                                Children = {
                                    new Text("Sizes") { ClassName = "text-sm font-medium text-muted-foreground" },
                                    new Flex {
                                        Gap = "1rem",
                                        Align = AlignItem.Center,
                                        Children = {
                                            new Avatar { Initials = "SM", Size = Size.Small },
                                            new Avatar { Initials = "MD", Size = Size.Medium },
                                            new Avatar { Initials = "LG", Size = Size.Large },
                                            new Avatar { Initials = "XL", Size = Size.XLarge }
                                        }
                                    },
                                    new Text("With Image") { ClassName = "text-sm font-medium text-muted-foreground mt-4" },
                                    new Flex {
                                        Gap = "1rem",
                                        Align = AlignItem.Center,
                                        Children = {
                                            new Avatar { ImageUrl = "https://i.pravatar.cc/40?img=1", AltText = "User 1", Size = Size.Small },
                                            new Avatar { ImageUrl = "https://i.pravatar.cc/48?img=2", AltText = "User 2", Size = Size.Medium },
                                            new Avatar { ImageUrl = "https://i.pravatar.cc/64?img=3", AltText = "User 3", Size = Size.Large }
                                        }
                                    },
                                    new Text("Initials Fallback") { ClassName = "text-sm font-medium text-muted-foreground mt-4" },
                                    new Flex {
                                        Gap = "1rem",
                                        Align = AlignItem.Center,
                                        Children = {
                                            new Avatar { Initials = "JD" },
                                            new Avatar { Initials = "AB" },
                                            new Avatar { Initials = "?" }
                                        }
                                    }
                                }
                            }
                        ),

                        // Spinner
                        CreateSection("Spinner", "Loading indicators.",
                            new Flex {
                                Gap = "2rem",
                                Align = AlignItem.Center,
                                Children = {
                                    new Box {
                                        ClassName = "text-center space-y-2",
                                        Children = {
                                            new Spinner { Variant = SpinnerVariant.Border, Size = SpinnerSize.Normal },
                                            new Text("Border") { ClassName = "text-xs text-muted-foreground" }
                                        }
                                    },
                                    new Box {
                                        ClassName = "text-center space-y-2",
                                        Children = {
                                            new Spinner { Variant = SpinnerVariant.Border, Size = SpinnerSize.Small },
                                            new Text("Small") { ClassName = "text-xs text-muted-foreground" }
                                        }
                                    },
                                    new Box {
                                        ClassName = "text-center space-y-2",
                                        Children = {
                                            new Spinner { Variant = SpinnerVariant.Grow, Size = SpinnerSize.Normal },
                                            new Text("Grow") { ClassName = "text-xs text-muted-foreground" }
                                        }
                                    },
                                    new Box {
                                        ClassName = "text-center space-y-2",
                                        Children = {
                                            new Spinner { Variant = SpinnerVariant.Grow, Size = SpinnerSize.Small },
                                            new Text("Grow Small") { ClassName = "text-xs text-muted-foreground" }
                                        }
                                    }
                                }
                            }
                        ),

                        // Heading
                        CreateSection("Heading", "Typography headings h1 through h6.",
                            new Box {
                                ClassName = "space-y-3",
                                Children = {
                                    new Heading("Heading 1", 1),
                                    new Heading("Heading 2", 2),
                                    new Heading("Heading 3", 3),
                                    new Heading("Heading 4", 4),
                                    new Heading("Heading 5", 5),
                                    new Heading("Heading 6", 6)
                                }
                            }
                        ),

                        // Text
                        CreateSection("Text", "Text component as span and paragraph.",
                            new Box {
                                ClassName = "space-y-3",
                                Children = {
                                    new Text("This is inline text (span)."),
                                    new Text("This is a paragraph of text. It renders as a <p> element with appropriate paragraph styling.") { Paragraph = true },
                                    new Text("Muted text for secondary information.") { ClassName = "text-muted-foreground" },
                                    new Text("Small text with reduced size.") { ClassName = "text-sm" },
                                    new Text("Bold emphasized text.") { ClassName = "font-bold" }
                                }
                            }
                        ),

                        // List
                        CreateSection("List", "Ordered and unordered lists.",
                            new Flex {
                                Gap = "2rem",
                                Children = {
                                    new Box {
                                        ClassName = "space-y-2",
                                        Children = {
                                            new Text("Unordered") { ClassName = "text-sm font-medium text-muted-foreground" },
                                            new List {
                                                Children = {
                                                    new ListItem { Children = { new Text("First item") } },
                                                    new ListItem { Children = { new Text("Second item") } },
                                                    new ListItem { Children = { new Text("Third item") } }
                                                }
                                            }
                                        }
                                    },
                                    new Box {
                                        ClassName = "space-y-2",
                                        Children = {
                                            new Text("Ordered") { ClassName = "text-sm font-medium text-muted-foreground" },
                                            new List {
                                                Ordered = true,
                                                Children = {
                                                    new ListItem { Children = { new Text("First item") } },
                                                    new ListItem { Children = { new Text("Second item") } },
                                                    new ListItem { Children = { new Text("Third item") } }
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
