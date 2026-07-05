using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Web.Components.Surfaces;

/// <summary>
/// Shadow levels for card elevation
/// </summary>
public enum Shadow
{
    None,
    Small,
    Medium,
    Large,
    XLarge
}

/// <summary>
/// Card component with support for both props-based and compound component patterns.
///
/// Props-based usage (legacy):
/// <code>
/// new Card {
///     Header = new Text("Title"),
///     Body = new Text("Content"),
///     Footer = new Button()
/// }
/// </code>
///
/// Compound components usage (recommended):
/// <code>
/// new Card {
///     Variant = CardVariant.Outline,
///     Children = {
///         new CardHeader {
///             Children = {
///                 new CardTitle { Text = "Title" },
///                 new CardDescription { Text = "Description" }
///             }
///         },
///         new CardBody {
///             Children = { new Text("Content") }
///         },
///         new CardFooter {
///             Children = { new Button { Text = "Action" } }
///         }
///     }
/// }
/// </code>
/// </summary>
public class Card : StatelessComponent
{
    /// <summary>
    /// [Legacy] Header component (for props-based usage)
    /// </summary>
    public IComponent? Header { get; set; }

    /// <summary>
    /// [Legacy] Body component (for props-based usage)
    /// </summary>
    public IComponent Body { get; set; } = new NullComponent();

    /// <summary>
    /// [Legacy] Footer component (for props-based usage)
    /// </summary>
    public IComponent? Footer { get; set; }

    /// <summary>
    /// Shadow level for card elevation (default: Medium)
    /// </summary>
    public Shadow Shadow { get; set; } = Shadow.Medium;

    /// <summary>
    /// Card visual variant (default: Default)
    /// </summary>
    public CardVariant Variant { get; set; } = CardVariant.Default;

    /// <summary>
    /// Width CSS class (default: w-full)
    /// </summary>
    public string Width { get; set; } = "w-full";

    public Card() { }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var cardTheme = theme?.Card;

        // Check if using compound components (new pattern) or props (legacy)
        var isCompoundPattern = Children.Any();

        // Build shadow class
        var shadowKey = Shadow.ToString().ToLower();
        var shadowClass = cardTheme?.GetShadowInfo(shadowKey) ?? "";

        // Build variant class
        var variantClass = cardTheme?.GetVariant(Variant) ?? $"eq-card-{Variant.ToString().ToLowerInvariant()}";

        // Build container class list
        var classes = new List<string>();
        if (!string.IsNullOrEmpty(cardTheme?.Container)) classes.Add(cardTheme.Container);
        else classes.Add("eq-card");
        if (!string.IsNullOrEmpty(variantClass)) classes.Add(variantClass);
        if (!string.IsNullOrEmpty(shadowClass)) classes.Add(shadowClass);
        if (!string.IsNullOrEmpty(Width)) classes.Add(Width);
        if (!string.IsNullOrEmpty(ClassName)) classes.Add(ClassName);

        var cardContainer = new Box
        {
            ClassName = string.Join(" ", classes),
            Children = { }
        };

        if (isCompoundPattern)
        {
            // New pattern: Use Children directly (compound components)
            foreach (var child in Children)
            {
                cardContainer.Children.Add(child);
            }
        }
        else
        {
            // Legacy pattern: Use Header/Body/Footer props
            var headerClass = cardTheme?.Header ?? "eq-card-header";
            var bodyClass = cardTheme?.Body ?? "eq-card-body";
            var footerClass = cardTheme?.Footer ?? "eq-card-footer";

            if (Header != null)
            {
                cardContainer.Children.Add(new Box
                {
                    ClassName = headerClass,
                    Children = { Header }
                });
            }

            if (Body != null)
            {
                cardContainer.Children.Add(new Box
                {
                    ClassName = bodyClass,
                    Children = { Body }
                });
            }

            if (Footer != null)
            {
                cardContainer.Children.Add(new Box
                {
                    ClassName = footerClass,
                    Children = { Footer }
                });
            }
        }

        return cardContainer;
    }
}
