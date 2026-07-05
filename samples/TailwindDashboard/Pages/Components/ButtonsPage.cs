using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;
using eQuantic.UI.Web.Components;
using eQuantic.UI.Web.Components.Layout;
using eQuantic.UI.Web.Components.Display;
using TailwindDashboard.Components;

namespace TailwindDashboard.Pages.Components;

[Page("/components/buttons", Title = "Buttons")]
public class ButtonsPage : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new DashboardShell
        {
            PageTitle = "Buttons",
            ActivePath = "/components/buttons",
            Children = {
                new Box {
                    ClassName = "space-y-12",
                    Children = {
                        // Variants
                        CreateSection("Variants", "All available button variants.",
                            new Flex {
                                Wrap = true, Gap = "0.5rem",
                                Align = AlignItem.Center,
                                Children = {
                                    new Button { Text = "Primary", Variant = Variant.Primary },
                                    new Button { Text = "Secondary", Variant = Variant.Secondary },
                                    new Button { Text = "Destructive", Variant = Variant.Destructive },
                                    new Button { Text = "Outline", Variant = Variant.Outline },
                                    new Button { Text = "Ghost", Variant = Variant.Ghost },
                                    new Button { Text = "Link", Variant = Variant.Link },
                                    new Button { Text = "Success", Variant = Variant.Success },
                                    new Button { Text = "Warning", Variant = Variant.Warning },
                                    new Button { Text = "Info", Variant = Variant.Info }
                                }
                            },
                            """
                            new Button { Text = "Primary", Variant = Variant.Primary }
                            new Button { Text = "Secondary", Variant = Variant.Secondary }
                            new Button { Text = "Destructive", Variant = Variant.Destructive }
                            new Button { Text = "Outline", Variant = Variant.Outline }
                            new Button { Text = "Ghost", Variant = Variant.Ghost }
                            new Button { Text = "Link", Variant = Variant.Link }
                            """
                        ),

                        // Sizes
                        CreateSection("Sizes", "Buttons in all available sizes.",
                            new Flex {
                                Wrap = true, Gap = "0.5rem",
                                Align = AlignItem.Center,
                                Children = {
                                    new Button { Text = "Small", Size = SizeVariant.Small },
                                    new Button { Text = "Medium", Size = SizeVariant.Medium },
                                    new Button { Text = "Large", Size = SizeVariant.Large },
                                    new Button { Text = "XLarge", Size = SizeVariant.XLarge }
                                }
                            },
                            """
                            new Button { Text = "Small", Size = SizeVariant.Small }
                            new Button { Text = "Medium", Size = SizeVariant.Medium }
                            new Button { Text = "Large", Size = SizeVariant.Large }
                            new Button { Text = "XLarge", Size = SizeVariant.XLarge }
                            """
                        ),

                        // Sizes per variant
                        CreateSection("Outline Sizes", "Outline variant in all sizes.",
                            new Flex {
                                Wrap = true, Gap = "0.5rem",
                                Align = AlignItem.Center,
                                Children = {
                                    new Button { Text = "Small", Variant = Variant.Outline, Size = SizeVariant.Small },
                                    new Button { Text = "Medium", Variant = Variant.Outline, Size = SizeVariant.Medium },
                                    new Button { Text = "Large", Variant = Variant.Outline, Size = SizeVariant.Large },
                                    new Button { Text = "XLarge", Variant = Variant.Outline, Size = SizeVariant.XLarge }
                                }
                            },
                            """
                            new Button { Text = "Small", Variant = Variant.Outline, Size = SizeVariant.Small }
                            new Button { Text = "Large", Variant = Variant.Outline, Size = SizeVariant.Large }
                            """
                        ),

                        // Loading state
                        CreateSection("Loading State", "Buttons with loading spinner.",
                            new Flex {
                                Wrap = true, Gap = "0.5rem",
                                Align = AlignItem.Center,
                                Children = {
                                    new Button { Text = "Loading...", Loading = true, Variant = Variant.Primary },
                                    new Button { Text = "Loading...", Loading = true, Variant = Variant.Secondary },
                                    new Button { Text = "Loading...", Loading = true, Variant = Variant.Outline }
                                }
                            },
                            """
                            new Button { Text = "Loading...", Loading = true, Variant = Variant.Primary }
                            """
                        ),

                        // Disabled state
                        CreateSection("Disabled State", "Buttons in disabled state.",
                            new Flex {
                                Wrap = true, Gap = "0.5rem",
                                Align = AlignItem.Center,
                                Children = {
                                    new Button { Text = "Primary", Disabled = true, Variant = Variant.Primary },
                                    new Button { Text = "Secondary", Disabled = true, Variant = Variant.Secondary },
                                    new Button { Text = "Destructive", Disabled = true, Variant = Variant.Destructive },
                                    new Button { Text = "Outline", Disabled = true, Variant = Variant.Outline },
                                    new Button { Text = "Ghost", Disabled = true, Variant = Variant.Ghost }
                                }
                            },
                            """
                            new Button { Text = "Primary", Disabled = true, Variant = Variant.Primary }
                            """
                        ),

                        // With children (icon + text)
                        CreateSection("With Icon", "Buttons with custom children content.",
                            new Flex {
                                Wrap = true, Gap = "0.5rem",
                                Align = AlignItem.Center,
                                Children = {
                                    new Button {
                                        Variant = Variant.Primary,
                                        Children = { new Text("📧"), new Text(" Send Email") }
                                    },
                                    new Button {
                                        Variant = Variant.Destructive,
                                        Children = { new Text("🗑️"), new Text(" Delete") }
                                    },
                                    new Button {
                                        Variant = Variant.Outline,
                                        Children = { new Text("⬇️"), new Text(" Download") }
                                    },
                                    new Button {
                                        Variant = Variant.Ghost,
                                        Children = { new Text("⚙️"), new Text(" Settings") }
                                    }
                                }
                            },
                            """
                            new Button {
                                Variant = Variant.Primary,
                                Children = {
                                    LucideIcons.Mail(size: 16),
                                    new Text(" Send Email")
                                }
                            }
                            """
                        ),

                        // Button types
                        CreateSection("Button Types", "HTML button type attribute.",
                            new Flex {
                                Wrap = true, Gap = "0.5rem",
                                Align = AlignItem.Center,
                                Children = {
                                    new Button { Text = "Button (default)", Type = "button" },
                                    new Button { Text = "Submit", Type = "submit", Variant = Variant.Primary },
                                    new Button { Text = "Reset", Type = "reset", Variant = Variant.Outline }
                                }
                            },
                            """
                            new Button { Text = "Submit", Type = "submit", Variant = Variant.Primary }
                            new Button { Text = "Reset", Type = "reset", Variant = Variant.Outline }
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
