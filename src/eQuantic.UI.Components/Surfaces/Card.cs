using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Surfaces;

public enum Shadow
{
    None,
    Small,
    Medium,
    Large,
    XLarge
}

public class Card : StatelessComponent
{
    public IComponent? Header { get; set; }
    public IComponent Body { get; set; } = new NullComponent();
    public IComponent? Footer { get; set; }
    public Shadow Shadow { get; set; } = Shadow.Medium;
    public string Width { get; set; } = "w-full";

    public Card(IComponent body)
    {
        Body = body;
    }
    
    // Parameterless constructor for composition
    public Card() { }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<eQuantic.UI.Core.Theme.IAppTheme>();
        var cardTheme = theme?.Card;

        var shadowClass = cardTheme?.GetShadowInfo(Shadow.ToString().ToLower()) ?? "";
        
        var containerClass = cardTheme?.Container ?? "";
        var headerClass = cardTheme?.Header ?? "";
        var bodyClass = cardTheme?.Body ?? "";
        var footerClass = cardTheme?.Footer ?? "";

        var cardContainer = new Box
        {
            ClassName = $"{containerClass} {shadowClass} {Width} {ClassName}",
            Children = { }
        };

        if (Header != null)
        {
            cardContainer.Children.Add(new Box
            {
                ClassName = headerClass,
                Children = { Header }
            });
        }

        cardContainer.Children.Add(new Box
        {
            ClassName = bodyClass,
            Children = { Body ?? new Text("") }
        });

        if (Footer != null)
        {
            cardContainer.Children.Add(new Box
            {
                ClassName = footerClass,
                Children = { Footer }
            });
        }

        return cardContainer;
    }
}
