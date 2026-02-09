using eQuantic.UI.Core;
using eQuantic.UI.Components;
using eQuantic.UI.Components.Display;
using eQuantic.UI.Components.Layout;
using eQuantic.UI.Components.Surfaces;
using TailwindDashboard.Components;

namespace TailwindDashboard.Pages;

[Page("/images")]
public class ImagesPage : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new DashboardShell
        {
            PageTitle = "Images",
            Children =
            {
                new Container
                {
                    ClassName = "grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 p-6",
                    Children =
                    {
                        new Card
                        {
                            Children =
                            {
                                new CardHeader
                                {
                                    Children =
                                    {
                                        new CardTitle { Text = "Basic Image" },
                                        new CardDescription { Text = "Fixed dimensions with lazy loading" }
                                    }
                                },
                                new CardBody
                                {
                                    Children =
                                    {
                                        new Image
                                        {
                                            Src = "https://images.unsplash.com/photo-1498050108023-c5249f4df085?auto=format&fit=crop&w=800&q=80",
                                            Alt = "Laptop on desk",
                                            Width = 400,
                                            Height = 250,
                                            ClassName = "rounded-lg"
                                        }
                                    }
                                }
                            }
                        },
                        new Card
                        {
                            Children =
                            {
                                new CardHeader
                                {
                                    Children =
                                    {
                                        new CardTitle { Text = "Blur Placeholder" },
                                        new CardDescription { Text = "Smooth transition using BlurDataURL" }
                                    }
                                },
                                new CardBody
                                {
                                    Children =
                                    {
                                        new Image
                                        {
                                            Src = "https://images.unsplash.com/photo-1461749280684-dccba630e2f6?auto=format&fit=crop&w=800&q=80",
                                            Alt = "Coding screen",
                                            Width = 400,
                                            Height = 250,
                                            Placeholder = ImagePlaceholder.Blur,
                                            BlurDataURL = "data:image/webp;base64,UklGRmAAAABXRUJQVlA4IFQAAADwAQCdASoKAAoAAUAmJaQAA3AA/vdaAAAA",
                                            ClassName = "rounded-lg"
                                        }
                                    }
                                }
                            }
                        },
                        new Card
                        {
                            Children =
                            {
                                new CardHeader
                                {
                                    Children =
                                    {
                                        new CardTitle { Text = "Fill Mode" },
                                        new CardDescription { Text = "Absolute positioning within parent container" }
                                    }
                                },
                                new CardBody
                                {
                                    Children =
                                    {
                                        new DynamicElement
                                        {
                                            TagName = "div",
                                            ClassName = "h-48 relative rounded-lg overflow-hidden",
                                            Children =
                                            {
                                                new Image
                                                {
                                                    Src = "https://images.unsplash.com/photo-1555066931-4365d14bab8c?auto=format&fit=crop&w=800&q=80",
                                                    Alt = "Code logic",
                                                    Fill = true,
                                                    Priority = true
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
