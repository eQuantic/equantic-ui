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

        var shadowKey = Shadow.ToString().ToLower();
        var shadowClass = "";
        if (cardTheme?.Shadows != null && cardTheme.Shadows.TryGetValue(shadowKey, out var s))
        {
            shadowClass = s;
        }
        
        var containerClass = cardTheme?.Container ?? "";
        var headerClass = cardTheme?.Header ?? "";
        var bodyClass = cardTheme?.Body ?? "";
        var footerClass = cardTheme?.Footer ?? "";

        // Build container class list safely to avoid "undefined" in JS
        var classes = new List<string>();
        if (!string.IsNullOrEmpty(containerClass)) classes.Add(containerClass);
        if (!string.IsNullOrEmpty(shadowClass)) classes.Add(shadowClass);
        if (!string.IsNullOrEmpty(Width)) classes.Add(Width);
        if (!string.IsNullOrEmpty(ClassName)) classes.Add(ClassName);
        
        var cardContainer = new Box
        {
            ClassName = string.Join(" ", classes),
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
