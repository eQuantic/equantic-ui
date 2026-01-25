using System.Collections.Generic;
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
        var cardTheme = theme != null ? theme.Card : null;

        var shadowKey = this.Shadow.ToString().ToLower();
        var shadowClass = "";
        if (cardTheme != null && cardTheme.Shadows != null)
        {
             var s = cardTheme.Shadows[shadowKey];
             if (s != null) shadowClass = s;
        }

        var containerClass = "";
        if (cardTheme != null) containerClass = cardTheme.Container;

        var headerClass = "";
        if (cardTheme != null) headerClass = cardTheme.Header;

        var bodyClass = "";
        if (cardTheme != null) bodyClass = cardTheme.Body;

        var footerClass = "";
        if (cardTheme != null) footerClass = cardTheme.Footer;

        // Build container class list safely to avoid "undefined" in JS
        var classes = new List<string>();
        if (!string.IsNullOrEmpty(containerClass)) classes.Add(containerClass);
        if (!string.IsNullOrEmpty(shadowClass)) classes.Add(shadowClass);
        if (!string.IsNullOrEmpty(this.Width)) classes.Add(this.Width);
        if (!string.IsNullOrEmpty(this.ClassName)) classes.Add(this.ClassName);

        var cardContainer = new Box
        {
            ClassName = string.Join(" ", classes),
            Children = { }
        };

        if (this.Header != null)
        {
            cardContainer.Children.Add(new Box
            {
                ClassName = headerClass,
                Children = { this.Header }
            });
        }

        var textBody = new Text("");
        if (this.Body != null)
        {
            cardContainer.Children.Add(new Box
            {
                ClassName = bodyClass,
                Children = { this.Body }
            });
        }
        else
        {
             cardContainer.Children.Add(new Box
            {
                ClassName = bodyClass,
                Children = { textBody }
            });
        }

        if (this.Footer != null)
        {
            cardContainer.Children.Add(new Box
            {
                ClassName = footerClass,
                Children = { this.Footer }
            });
        }

        return cardContainer;
    }
}
