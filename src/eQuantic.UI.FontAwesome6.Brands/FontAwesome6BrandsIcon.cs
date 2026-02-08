using eQuantic.UI.Core;
using eQuantic.UI.Components;

namespace eQuantic.UI.FontAwesome6Brands;

public class FontAwesome6BrandsIcon : StatelessComponent
{
    public string? Name { get; set; }
    public int Size { get; set; } = 24;
    public double StrokeWidth { get; set; } = 2;
    public string Color { get; set; } = "currentColor";
    public string ViewBox { get; set; } = "0 0 24 24";
    public List<IComponent> Content { get; set; } = new();

    public override IComponent Build(RenderContext context)
    {
        var svg = new DynamicElement
        {
            TagName = "svg",
            CustomAttributes = new Dictionary<string, string>
            {
                ["xmlns"] = "http://www.w3.org/2000/svg",
                ["width"] = Size.ToString(),
                ["height"] = Size.ToString(),
                ["viewBox"] = ViewBox,
                ["fill"] = "none",
                ["stroke"] = "currentColor",
                ["stroke-width"] = StrokeWidth.ToString(),
                ["stroke-linecap"] = "round",
                ["stroke-linejoin"] = "round",
                ["style"] = $"color: {Color}",
                ["class"] = $"icon icon-fa6-brands icon-fa6-brands-{Name} {ClassName}".Trim()
            }
        };

        foreach (var item in Content)
        {
            svg.Children.Add(item);
        }

        return svg;
    }
}
