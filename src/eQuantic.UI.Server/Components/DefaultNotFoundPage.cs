using eQuantic.UI.Core;
using eQuantic.UI.Components;

namespace eQuantic.UI.Server.Components;

/// <summary>
/// Default 404 Not Found page.
/// </summary>
[Page("/404", Title = "404 - Page Not Found", DisableSsr = false)]
public class DefaultNotFoundPage : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new Container
        {
            Fluid = true,
            Children = 
            {
                new Flex
                {
                    Direction = FlexDirection.Column,
                    Justify = JustifyContent.Center,
                    Align = AlignItem.Center,
                    Gap = "1rem",
                    Style = new HtmlStyle { Height = "100vh", FontFamily = "system-ui, sans-serif" },
                    Children = 
                    {
                        new Text("404") 
                        { 
                            ClassName = "eq-text-6xl eq-font-bold",
                            Style = new HtmlStyle { FontSize = "3rem", FontWeight = "bold" }
                        },
                        new Text("Page Not Found")
                        {
                            Style = new HtmlStyle { FontSize = "1.5rem" }
                        },
                        new Link { Href = "/", Text = "Go Home", Style = new HtmlStyle { Color = "#6366f1", TextDecoration = "underline", MarginTop = "2rem" } }
                    }
                }
            }
        };
    }
}
