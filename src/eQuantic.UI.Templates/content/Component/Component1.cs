using eQuantic.UI.Core;
using eQuantic.UI.Components;

namespace EQuanticApp.Components;

public class Component1 : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new Container
        {
            Children = {
                new Text("Component1 works!")
            }
        };
    }
}
