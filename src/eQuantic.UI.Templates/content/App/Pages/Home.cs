using eQuantic.UI.Core;
using eQuantic.UI.Components;

namespace EQuanticApp.Pages;

[Page("/", Title = "Home")]
public class Home : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new Container
        {
            ClassName = "container",
            Children = {
                new Heading("Welcome to eQuantic.UI", 1),
                new Text("This is a sample application running on .NET 8."),
                new Button {
                    Text = "Click Me",
                    ClassName = "btn btn-primary",
                    OnClick = () => Console.WriteLine("Clicked!")
                }
            }
        };
    }
}
