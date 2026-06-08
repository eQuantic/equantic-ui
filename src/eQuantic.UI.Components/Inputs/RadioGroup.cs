using System;
using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;
using eQuantic.UI.Components.Layout;

namespace eQuantic.UI.Components.Inputs;

public class RadioGroup : InputComponent<string>
{
    public string Name { get; set; } = Guid.NewGuid().ToString("N");
    public List<RadioOption> Options { get; set; } = new();
    public FlexDirection Direction { get; set; } = FlexDirection.Column;

    public override IComponent Build(RenderContext context)
    {
        var container = new Box { As = "div" };

        // Forward all host attributes (id, title, tabindex, class, ARIA, data-*, events, ...)
        // so nothing set on the RadioGroup itself is lost when delegating to the Box.
        CopyHtmlAttributesTo(container);

        // Container layout: merge the flex layout into any forwarded inline style.
        var style = container.Style ?? new HtmlStyle();
        style.Display = eQuantic.UI.Core.Display.Flex;
        style.FlexDirection = Direction;
        if (string.IsNullOrEmpty(style.Gap)) style.Gap = "0.5rem";
        container.Style = style;

        container.AriaOrientation =
            (Direction == FlexDirection.Row || Direction == FlexDirection.RowReverse) ? "horizontal" : "vertical";

        if (Options.Any())
        {
            foreach (var opt in Options)
            {
                container.Children.Add(new Radio
                {
                    Name = Name,
                    Value = opt.Value,
                    Label = opt.Label,
                    Disabled = opt.Disabled,
                    Checked = Value == opt.Value,
                    OnChange = OnChange
                });
            }
        }
        else
        {
            if (Children.Any())
            {
                foreach (var child in Children)
                {
                     container.Children.Add(child);
                }
            }
        }

        return container;
    }
}
