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
        var shadowClass = Shadow switch
        {
            Shadow.None => "shadow-none",
            Shadow.Small => "shadow-sm",
            Shadow.Medium => "shadow-md",
            Shadow.Large => "shadow-lg",
            Shadow.XLarge => "shadow-xl",
            _ => "shadow-md"
        };
        
        var cardContainer = new Column
        {
            ClassName = $"bg-white dark:bg-zinc-800 rounded-lg {shadowClass} overflow-hidden border border-gray-200 dark:border-zinc-700 {Width} {ClassName}",
            Children = { }
        };

        if (Header != null)
        {
            cardContainer.Children.Add(new Container
            {
                ClassName = "px-6 py-4 border-b border-gray-200 dark:border-zinc-700 bg-gray-50 dark:bg-zinc-800/50",
                Children = { Header }
            });
        }

        cardContainer.Children.Add(new Container
        {
            ClassName = "p-6",
            Children = { Body ?? new Text("") }
        });

        if (Footer != null)
        {
            cardContainer.Children.Add(new Container
            {
                ClassName = "px-6 py-4 bg-gray-50 dark:bg-zinc-800/50 border-t border-gray-200 dark:border-zinc-700",
                Children = { Footer }
            });
        }

        return cardContainer;
    }
}
