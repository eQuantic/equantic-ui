using eQuantic.UI.Core;
using eQuantic.UI.Web.Components;

namespace eQuantic.UI.Server.Components;

/// <summary>
/// Default 500 Internal Server Error page.
/// </summary>
[Page("/500", Title = "500 - Internal Server Error", DisableSsr = false)]
public class DefaultErrorPage : StatelessComponent
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
                        new Text("500") 
                        { 
                            ClassName = "eq-text-6xl eq-font-bold",
                            Style = new HtmlStyle { FontSize = "3rem", FontWeight = "bold" }
                        },
                        new Text("Internal Server Error")
                        {
                            Style = new HtmlStyle { FontSize = "1.5rem" }
                        },
                        new Text("Sorry, something went wrong on our end.")
                        {
                            Style = new HtmlStyle { Color = "#64748b" }
                        }
                    }
                }
            }
        };
    }
}
