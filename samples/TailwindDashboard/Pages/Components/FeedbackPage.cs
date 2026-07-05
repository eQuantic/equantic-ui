using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;
using eQuantic.UI.Web.Components;
using eQuantic.UI.Web.Components.Layout;
using eQuantic.UI.Web.Components.Display;
using eQuantic.UI.Web.Components.Feedback;
using TailwindDashboard.Components;

namespace TailwindDashboard.Pages.Components;

[Page("/components/feedback", Title = "Feedback")]
public class FeedbackPage : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new DashboardShell
        {
            PageTitle = "Feedback",
            ActivePath = "/components/feedback",
            Children = {
                new Box {
                    ClassName = "space-y-12",
                    Children = {
                        // Alert - Default
                        CreateSection("Alert - Default", "Standard alert with title and description.",
                            new Box {
                                ClassName = "space-y-4 max-w-lg",
                                Children = {
                                    new Alert {
                                        Variant = Variant.Default,
                                        Children = {
                                            new AlertTitle { Children = { new Text("Heads up!") } },
                                            new AlertDescription { Children = { new Text("You can add components to your app using the CLI.") } }
                                        }
                                    }
                                }
                            },
                            """
                            new Alert {
                                Variant = Variant.Default,
                                Children = {
                                    new AlertTitle { Children = { new Text("Heads up!") } },
                                    new AlertDescription { Children = { new Text("Description here.") } }
                                }
                            }
                            """
                        ),

                        // Alert - All Variants
                        CreateSection("Alert Variants", "Alerts with different semantic colors.",
                            new Box {
                                ClassName = "space-y-4 max-w-lg",
                                Children = {
                                    new Alert {
                                        Variant = Variant.Primary,
                                        Children = {
                                            new AlertTitle { Children = { new Text("Primary") } },
                                            new AlertDescription { Children = { new Text("This is a primary alert for important information.") } }
                                        }
                                    },
                                    new Alert {
                                        Variant = Variant.Success,
                                        Children = {
                                            new AlertTitle { Children = { new Text("Success") } },
                                            new AlertDescription { Children = { new Text("Your changes have been saved successfully.") } }
                                        }
                                    },
                                    new Alert {
                                        Variant = Variant.Warning,
                                        Children = {
                                            new AlertTitle { Children = { new Text("Warning") } },
                                            new AlertDescription { Children = { new Text("Your session is about to expire. Please save your work.") } }
                                        }
                                    },
                                    new Alert {
                                        Variant = Variant.Destructive,
                                        Children = {
                                            new AlertTitle { Children = { new Text("Error") } },
                                            new AlertDescription { Children = { new Text("Something went wrong. Please try again later.") } }
                                        }
                                    },
                                    new Alert {
                                        Variant = Variant.Info,
                                        Children = {
                                            new AlertTitle { Children = { new Text("Info") } },
                                            new AlertDescription { Children = { new Text("A new software update is available. See what's new.") } }
                                        }
                                    },
                                    new Alert {
                                        Variant = Variant.Secondary,
                                        Children = {
                                            new AlertTitle { Children = { new Text("Secondary") } },
                                            new AlertDescription { Children = { new Text("A secondary alert for less prominent notifications.") } }
                                        }
                                    },
                                    new Alert {
                                        Variant = Variant.Outline,
                                        Children = {
                                            new AlertTitle { Children = { new Text("Outline") } },
                                            new AlertDescription { Children = { new Text("An outline-styled alert for subtle notifications.") } }
                                        }
                                    }
                                }
                            },
                            """
                            new Alert {
                                Variant = Variant.Success,
                                Children = {
                                    new AlertTitle { Children = { new Text("Success") } },
                                    new AlertDescription { Children = { new Text("Saved!") } }
                                }
                            }
                            new Alert { Variant = Variant.Destructive, ... }
                            new Alert { Variant = Variant.Warning, ... }
                            """
                        ),

                        // Alert without title
                        CreateSection("Alert - Description Only", "Alerts without a title.",
                            new Box {
                                ClassName = "space-y-4 max-w-lg",
                                Children = {
                                    new Alert {
                                        Variant = Variant.Info,
                                        Children = {
                                            new AlertDescription { Children = { new Text("This alert only has a description, no title.") } }
                                        }
                                    }
                                }
                            }
                        ),

                        // Toast
                        CreateSection("Toast", "Toast notification (preview - requires runtime interaction).",
                            new Box {
                                ClassName = "space-y-4",
                                Children = {
                                    new Text("Toasts appear as floating notifications. Below is a static preview.") { ClassName = "text-sm text-muted-foreground" },
                                    new Flex {
                                        Gap = "1rem",
                                        Wrap = true,
                                        Children = {
                                            new Box {
                                                ClassName = "relative p-4 rounded-lg border border-border bg-card shadow-lg max-w-sm",
                                                Children = {
                                                    new Text("Event Created") { ClassName = "font-semibold text-sm" },
                                                    new Text("Sunday, December 03, 2023 at 9:00 AM") { ClassName = "text-sm text-muted-foreground mt-1" }
                                                }
                                            },
                                            new Box {
                                                ClassName = "relative p-4 rounded-lg border border-red-200 bg-red-50 dark:border-red-900 dark:bg-red-950 shadow-lg max-w-sm",
                                                Children = {
                                                    new Text("Error") { ClassName = "font-semibold text-sm text-red-600 dark:text-red-400" },
                                                    new Text("There was a problem with your request.") { ClassName = "text-sm text-red-500 dark:text-red-300 mt-1" }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        ),

                        // Spinner (also feedback)
                        CreateSection("Spinner", "Loading indicators for async operations.",
                            new Flex {
                                Gap = "2rem",
                                Align = AlignItem.Center,
                                Children = {
                                    new Box {
                                        ClassName = "text-center space-y-2",
                                        Children = {
                                            new Spinner { Variant = SpinnerVariant.Border },
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
                                            new Spinner { Variant = SpinnerVariant.Grow },
                                            new Text("Grow") { ClassName = "text-xs text-muted-foreground" }
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
