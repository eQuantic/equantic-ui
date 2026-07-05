using eQuantic.UI.Core;
using eQuantic.UI.Web.Components;
using eQuantic.UI.Web.Components.Display;
using eQuantic.UI.Web.Components.Layout;
using eQuantic.UI.Web.Components.Surfaces;
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
                // Section: Server-Side Optimization
                new Container
                {
                    ClassName = "px-6 pt-6",
                    Children =
                    {
                        new Heading
                        {
                            Level = 2,
                            Content = "Server-Side Optimization",
                            ClassName = "text-xl font-semibold mb-2"
                        },
                        new Text
                        {
                            Content = "Local images are automatically resized, converted to WebP, and served with optimized srcset/sizes. Powered by eQuantic.UI.Images with SixLabors.ImageSharp.",
                            ClassName = "text-muted-foreground mb-4"
                        }
                    }
                },
                new Container
                {
                    ClassName = "grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 px-6",
                    Children =
                    {
                        // Fixed-size optimized image (1x/2x density descriptors)
                        new Card
                        {
                            Children =
                            {
                                new CardHeader
                                {
                                    Children =
                                    {
                                        new CardTitle { Text = "Optimized (Fixed Size)" },
                                        new CardDescription { Text = "Auto 1x/2x density descriptors, WebP format" }
                                    }
                                },
                                new CardBody
                                {
                                    Children =
                                    {
                                        new Image
                                        {
                                            Src = "/images/landscape.jpg",
                                            Alt = "Optimized landscape",
                                            Width = 400,
                                            Height = 267,
                                            ClassName = "rounded-lg"
                                        }
                                    }
                                }
                            }
                        },
                        // Responsive optimized image (width descriptors for all sizes)
                        new Card
                        {
                            Children =
                            {
                                new CardHeader
                                {
                                    Children =
                                    {
                                        new CardTitle { Text = "Optimized (Responsive)" },
                                        new CardDescription { Text = "Full srcset with all configured widths" }
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
                                                    Src = "/images/hero.jpg",
                                                    Alt = "Optimized hero",
                                                    Fill = true
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        },
                        // Custom quality
                        new Card
                        {
                            Children =
                            {
                                new CardHeader
                                {
                                    Children =
                                    {
                                        new CardTitle { Text = "Custom Quality (q=50)" },
                                        new CardDescription { Text = "Lower quality for smaller file sizes" }
                                    }
                                },
                                new CardBody
                                {
                                    Children =
                                    {
                                        new Image
                                        {
                                            Src = "/images/square.jpg",
                                            Alt = "Low quality square",
                                            Width = 400,
                                            Height = 400,
                                            Quality = 50,
                                            ClassName = "rounded-lg"
                                        }
                                    }
                                }
                            }
                        },
                        // Opt-out: explicit Optimize = false
                        new Card
                        {
                            Children =
                            {
                                new CardHeader
                                {
                                    Children =
                                    {
                                        new CardTitle { Text = "Opt-Out (No Optimization)" },
                                        new CardDescription { Text = "Optimize = false serves original file" }
                                    }
                                },
                                new CardBody
                                {
                                    Children =
                                    {
                                        new Image
                                        {
                                            Src = "/images/portrait.jpg",
                                            Alt = "Original portrait",
                                            Width = 200,
                                            Height = 300,
                                            Optimize = false,
                                            ClassName = "rounded-lg"
                                        }
                                    }
                                }
                            }
                        },
                        // Priority image (eager loading)
                        new Card
                        {
                            Children =
                            {
                                new CardHeader
                                {
                                    Children =
                                    {
                                        new CardTitle { Text = "Priority (Eager Load)" },
                                        new CardDescription { Text = "fetchpriority=high + loading=eager + optimized" }
                                    }
                                },
                                new CardBody
                                {
                                    Children =
                                    {
                                        new Image
                                        {
                                            Src = "/images/hero.jpg",
                                            Alt = "Priority hero",
                                            Width = 400,
                                            Height = 225,
                                            Priority = true,
                                            ClassName = "rounded-lg"
                                        }
                                    }
                                }
                            }
                        }
                    }
                },

                // Section: External Images (no optimization)
                new Container
                {
                    ClassName = "px-6 pt-8",
                    Children =
                    {
                        new Heading
                        {
                            Level = 2,
                            Content = "External Images",
                            ClassName = "text-xl font-semibold mb-2"
                        },
                        new Text
                        {
                            Content = "External URLs are never optimized (security). These use standard lazy loading and blur placeholders.",
                            ClassName = "text-muted-foreground mb-4"
                        }
                    }
                },
                new Container
                {
                    ClassName = "grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 px-6 pb-6",
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
