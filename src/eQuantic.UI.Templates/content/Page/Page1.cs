using eQuantic.UI.Core;
using eQuantic.UI.Components;

namespace EQuanticApp.Pages;

[Page("/page1", Title = "Page1")]
public class Page1 : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new Container
        {
            Children = {
                new Heading("Page1", 1),
                new Text("Welcome to your new page.")
            }
        };
    }
}
