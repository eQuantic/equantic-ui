using System.Collections.Generic;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;
using eQuantic.UI.Components;
using eQuantic.UI.Components.Layout;
using eQuantic.UI.Components.Display;
using eQuantic.UI.Components.Inputs;
using eQuantic.UI.Components.Forms;
using TailwindDashboard.Components;

namespace TailwindDashboard.Pages.Components;

[Page("/components/inputs", Title = "Inputs")]
public class InputsPage : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new DashboardShell
        {
            PageTitle = "Inputs",
            ActivePath = "/components/inputs",
            Children = {
                new Box {
                    ClassName = "space-y-12",
                    Children = {
                        // TextInput
                        CreateSection("Text Input", "Standard text input with different sizes and states.",
                            new Box {
                                ClassName = "space-y-6 max-w-md",
                                Children = {
                                    // Sizes
                                    new Text("Sizes") { ClassName = "text-sm font-medium text-muted-foreground" },
                                    new TextInput { Placeholder = "Small input", Size = Size.Small },
                                    new TextInput { Placeholder = "Medium input (default)", Size = Size.Medium },
                                    new TextInput { Placeholder = "Large input", Size = Size.Large },

                                    // Types
                                    new Text("Input Types") { ClassName = "text-sm font-medium text-muted-foreground mt-4" },
                                    new TextInput { Type = "text", Placeholder = "Text input" },
                                    new TextInput { Type = "email", Placeholder = "Email input" },
                                    new TextInput { Type = "password", Placeholder = "Password input" },
                                    new TextInput { Type = "number", Placeholder = "Number input" },
                                    new TextInput { Type = "search", Placeholder = "Search input" },

                                    // States
                                    new Text("States") { ClassName = "text-sm font-medium text-muted-foreground mt-4" },
                                    new TextInput { Placeholder = "Disabled input", Disabled = true },
                                    new TextInput { Value = "Read-only value", ReadOnly = true }
                                }
                            },
                            """
                            new TextInput { Placeholder = "Email", Type = "email", Size = Size.Medium }
                            new TextInput { Placeholder = "Disabled", Disabled = true }
                            new TextInput { Value = "Read-only", ReadOnly = true }
                            """
                        ),

                        // TextArea
                        CreateSection("Text Area", "Multi-line text input.",
                            new Box {
                                ClassName = "space-y-4 max-w-md",
                                Children = {
                                    new TextArea { Placeholder = "Default textarea", Rows = 3 },
                                    new TextArea { Placeholder = "Small textarea", Size = Size.Small, Rows = 2 },
                                    new TextArea { Placeholder = "Large textarea", Size = Size.Large, Rows = 4 },
                                    new TextArea { Placeholder = "Disabled textarea", Disabled = true, Rows = 3 }
                                }
                            },
                            """
                            new TextArea { Placeholder = "Write something...", Rows = 3 }
                            new TextArea { Placeholder = "Small", Size = Size.Small, Rows = 2 }
                            """
                        ),

                        // Checkbox
                        CreateSection("Checkbox", "Checkboxes in native and custom styles.",
                            new Box {
                                ClassName = "space-y-4",
                                Children = {
                                    new Text("Native Checkboxes") { ClassName = "text-sm font-medium text-muted-foreground" },
                                    new Flex {
                                        Gap = "1rem", Wrap = true,
                                        Children = {
                                            new Checkbox { IsNative = true, Name = "native1" },
                                            new Checkbox { IsNative = true, Checked = true, Name = "native2" },
                                            new Checkbox { IsNative = true, Disabled = true, Name = "native3" }
                                        }
                                    },
                                    new Text("Rich Checkboxes") { ClassName = "text-sm font-medium text-muted-foreground mt-4" },
                                    new Flex {
                                        Gap = "1rem", Wrap = true,
                                        Children = {
                                            new Checkbox { IsNative = false, Name = "rich1" },
                                            new Checkbox { IsNative = false, Checked = true, Name = "rich2" },
                                            new Checkbox { IsNative = false, Disabled = true, Name = "rich3" }
                                        }
                                    }
                                }
                            },
                            """
                            new Checkbox { IsNative = true, Checked = true, Name = "agree" }
                            new Checkbox { IsNative = false, Checked = true, Name = "terms" }
                            """
                        ),

                        // Switch
                        CreateSection("Switch", "Toggle switch components.",
                            new Flex {
                                Direction = FlexDirection.Column, Gap = "1rem",
                                Children = {
                                    new Switch { Label = "Notifications", Value = true },
                                    new Switch { Label = "Dark Mode", Value = false },
                                    new Switch { Label = "Disabled switch", Disabled = true }
                                }
                            },
                            """
                            new Switch { Label = "Notifications", Value = true }
                            new Switch { Label = "Dark Mode", Value = false }
                            """
                        ),

                        // Radio
                        CreateSection("Radio", "Radio button selection.",
                            new Flex {
                                Direction = FlexDirection.Column, Gap = "0.75rem",
                                Children = {
                                    new Radio { Label = "Option A", Name = "demo-radio", Value = "a", Checked = true },
                                    new Radio { Label = "Option B", Name = "demo-radio", Value = "b" },
                                    new Radio { Label = "Option C (disabled)", Name = "demo-radio", Value = "c", Disabled = true }
                                }
                            },
                            """
                            new Radio { Label = "Option A", Name = "plan", Value = "a", Checked = true }
                            new Radio { Label = "Option B", Name = "plan", Value = "b" }
                            """
                        ),

                        // Select
                        CreateSection("Select", "Dropdown select component.",
                            new Box {
                                ClassName = "space-y-4 max-w-md",
                                Children = {
                                    new Text("Native Select") { ClassName = "text-sm font-medium text-muted-foreground" },
                                    new Select {
                                        IsNative = true,
                                        Options = new List<SelectOption> {
                                            new SelectOption { Label = "Select an option...", Value = "" },
                                            new SelectOption { Label = "React", Value = "react" },
                                            new SelectOption { Label = "Vue", Value = "vue" },
                                            new SelectOption { Label = "Angular", Value = "angular" },
                                            new SelectOption { Label = "Svelte", Value = "svelte" }
                                        }
                                    },
                                    new Text("Disabled Select") { ClassName = "text-sm font-medium text-muted-foreground" },
                                    new Select {
                                        IsNative = true,
                                        Disabled = true,
                                        Options = new List<SelectOption> {
                                            new SelectOption { Label = "Disabled", Value = "" }
                                        }
                                    }
                                }
                            },
                            """
                            new Select {
                                IsNative = true,
                                Options = new List<SelectOption> {
                                    new SelectOption { Label = "React", Value = "react" },
                                    new SelectOption { Label = "Vue", Value = "vue" },
                                }
                            }
                            """
                        ),

                        // Slider
                        CreateSection("Slider", "Range slider input.",
                            new Box {
                                ClassName = "space-y-4 max-w-md",
                                Children = {
                                    new Slider { Value = 50, Min = 0, Max = 100 },
                                    new Slider { Value = 25, Min = 0, Max = 100, Step = 5 }
                                }
                            },
                            """
                            new Slider { Value = 50, Min = 0, Max = 100 }
                            new Slider { Value = 25, Min = 0, Max = 100, Step = 5 }
                            """
                        ),

                        // FormField
                        CreateSection("Form Field", "Input wrapped with label, helper text, and error states.",
                            new Box {
                                ClassName = "space-y-6 max-w-md",
                                Children = {
                                    new FormField {
                                        Label = "Email",
                                        For = "email",
                                        Required = true,
                                        HelperText = "We'll never share your email.",
                                        Children = {
                                            new TextInput { Type = "email", Placeholder = "you@example.com", Name = "email" }
                                        }
                                    },
                                    new FormField {
                                        Label = "Password",
                                        For = "password",
                                        Required = true,
                                        Children = {
                                            new TextInput { Type = "password", Placeholder = "Enter password", Name = "password" }
                                        }
                                    },
                                    new FormField {
                                        Label = "Username",
                                        For = "username",
                                        Error = "This username is already taken.",
                                        Children = {
                                            new TextInput { Placeholder = "johndoe", Name = "username" }
                                        }
                                    }
                                }
                            },
                            """
                            new FormField {
                                Label = "Email",
                                For = "email",
                                Required = true,
                                HelperText = "We'll never share your email.",
                                Children = {
                                    new TextInput { Type = "email", Placeholder = "you@example.com" }
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
            section.Children.Add(new CodeBlock(code.Trim(), "csharp"));
        }

        return section;
    }
}
