using eQuantic.UI.Core;
using eQuantic.UI.Web.Components;
using eQuantic.UI.Web.Components.Layout;
using eQuantic.UI.Web.Components.Surfaces;
using eQuantic.UI.Core.Theme.Types;
using eQuantic.UI.Material;

namespace MaterialDashboard.Pages;

[Page("/showcase", Title = "Material Showcase")]
public class Showcase : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new Box
        {
            ClassName = "eq-min-h-screen eq-surface-subtle eq-text-primary",
            Children = {
                // Header (Mock)
                new Box {
                    ClassName = "eq-h-16 eq-flex eq-items-center eq-px-6 eq-border-b eq-surface eq-sticky eq-top-0 eq-z-50",
                    Children = {
                        new Link { Href = "/", ClassName = "material-icons eq-text-secondary", Children = { new Text("arrow_back") } },
                        new Text("Material Components") { ClassName = $"eq-ml-4 {M3.Typography.TitleLarge}" }
                    }
                },
                new Box {
                    ClassName = "eq-p-8 eq-max-w-4xl eq-mx-auto eq-space-y-12",
                    Children = {
                        // Buttons
                        CreateSection("Common Buttons", new Box {
                            ClassName = "eq-flex eq-flex-wrap eq-gap-4",
                            Children = {
                                new Button { Text = "Elevated", Variant = Variant.Default },
                                new Button { Text = "Filled", Variant = Variant.Primary },
                                new Button { Text = "Tonal", Variant = Variant.Secondary },
                                new Button { Text = "Outlined", Variant = Variant.Outline },
                                new Button { Text = "Text", Variant = Variant.Ghost }
                            }
                        }),

                        // Icon Buttons
                        CreateSection("Icon Buttons", new Box {
                            ClassName = "eq-flex eq-flex-wrap eq-gap-4",
                            Children = {
                                new Button { ClassName = "eq-p-2", Children = { new Text("favorite") { ClassName = "material-icons" } }, Variant = Variant.Primary },
                                new Button { ClassName = "eq-p-2", Children = { new Text("share") { ClassName = "material-icons" } }, Variant = Variant.Outline },
                                new Button { ClassName = "eq-p-2", Children = { new Text("more_vert") { ClassName = "material-icons" } }, Variant = Variant.Ghost }
                            }
                        }),

                        // Cards
                        CreateSection("Cards", new Grid {
                            Columns = 2,
                            Gap = "1.5rem",
                            Children = {
                                new Card {
                                    Variant = CardVariant.Elevated,
                                    Children = {
                                        new Box {
                                            ClassName = "p-4",
                                            Children = {
                                                new Heading("Elevated Card", 3) { ClassName = $"{M3.Typography.TitleLarge} eq-mb-2" },
                                                new Text("Elevated cards provide more separation from the background than filled cards.") { ClassName = $"{M3.Typography.BodyMedium} eq-text-secondary" }
                                            }
                                        }
                                    }
                                },
                                new Card {
                                    Variant = CardVariant.Outline,
                                    Children = {
                                        new Box {
                                            ClassName = "p-4",
                                            Children = {
                                                new Heading("Outlined Card", 3) { ClassName = $"{M3.Typography.TitleLarge} eq-mb-2" },
                                                new Text("Outlined cards provide clean separation without elevation.") { ClassName = $"{M3.Typography.BodyMedium} eq-text-secondary" }
                                            }
                                        }
                                    }
                                }
                            }
                        }),

                        // FAB Variants
                        CreateSection("Floating Action Buttons", new Box {
                            ClassName = "eq-flex eq-flex-wrap eq-items-center eq-gap-6",
                            Children = {
                                new Button { ClassName = "md-fab", Children = { new Text("edit") { ClassName = "material-icons" } } },
                                new Button { ClassName = "md-fab md-fab--extended", Children = { new Text("add") { ClassName = "material-icons eq-mr-2" }, new Text("Create New") } }
                            }
                        })
                    }
                }
            }
        };
    }

    private IComponent CreateSection(string title, IComponent content)
    {
        return new Box {
            ClassName = "space-y-4",
            Children = {
                new Heading(title, 2) { ClassName = $"{M3.Typography.LabelLarge} eq-text-secondary eq-uppercase eq-tracking-widest" },
                new Box {
                    ClassName = "eq-p-8 eq-rounded-3xl eq-surface eq-border eq-shadow-sm",
                    Children = { content }
                }
            }
        };
    }
}
