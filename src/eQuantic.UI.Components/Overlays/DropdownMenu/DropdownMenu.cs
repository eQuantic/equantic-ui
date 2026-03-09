using System;
using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Overlays.DropdownMenu;

/// <summary>
/// A menu that appears when triggered, displaying a list of actions or options.
///
/// Usage:
/// <code>
/// new DropdownMenu {
///     Trigger = new Button { Text = "Options", Variant = Variant.Outline },
///     Children = {
///         new DropdownMenuLabel { Text = "My Account" },
///         new DropdownMenuSeparator(),
///         new DropdownMenuItem { Text = "Profile", Shortcut = "⇧⌘P" },
///         new DropdownMenuItem { Text = "Settings", Shortcut = "⌘S" },
///         new DropdownMenuSeparator(),
///         new DropdownMenuItem { Text = "Log out", Shortcut = "⇧⌘Q" }
///     }
/// }
/// </code>
/// </summary>
public class DropdownMenu : StatelessComponent
{
    /// <summary>The trigger element that opens the menu.</summary>
    public IComponent? Trigger { get; set; }

    /// <summary>Whether the menu is open (controlled mode). Default: false.</summary>
    public bool IsOpen { get; set; }

    /// <summary>Side to display the menu. Default: Bottom.</summary>
    public Side Side { get; set; } = Side.Bottom;

    /// <summary>Alignment of the menu. Default: Start.</summary>
    public Align Align { get; set; } = Align.Start;

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var menuTheme = theme?.DropdownMenu;

        var id = Id ?? $"dropdown-{Guid.NewGuid():N}";
        var contentClass = menuTheme?.Content ?? "eq-dropdown-content";

        var container = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = $"eq-dropdown {ClassName}".Trim(),
                ["data-state"] = IsOpen ? "open" : "closed"
            }
        };

        // Trigger
        if (Trigger != null)
        {
            var triggerWrapper = new DynamicElement
            {
                TagName = "button",
                CustomAttributes = new Dictionary<string, string>
                {
                    ["class"] = "eq-dropdown-trigger",
                    ["type"] = "button",
                    ["aria-haspopup"] = "menu",
                    ["aria-expanded"] = IsOpen ? "true" : "false",
                    ["data-state"] = IsOpen ? "open" : "closed"
                }
            };
            triggerWrapper.Children.Add(Trigger);
            container.Children.Add(triggerWrapper);
        }

        // Content
        if (IsOpen)
        {
            var content = new DynamicElement
            {
                TagName = "div",
                CustomAttributes = new Dictionary<string, string>
                {
                    ["class"] = contentClass,
                    ["role"] = "menu",
                    ["aria-orientation"] = "vertical",
                    ["data-state"] = "open",
                    ["data-side"] = Side.ToString().ToLowerInvariant(),
                    ["data-align"] = Align.ToString().ToLowerInvariant(),
                    ["tabindex"] = "-1"
                }
            };

            foreach (var child in Children)
            {
                if (child is IDropdownMenuChild menuChild)
                {
                    menuChild.MenuTheme = menuTheme;
                }
                content.Children.Add(child);
            }

            container.Children.Add(content);
        }

        return container;
    }
}

/// <summary>Interface for dropdown menu child components.</summary>
public interface IDropdownMenuChild : IComponent
{
    Core.Theme.IDropdownMenuTheme? MenuTheme { get; set; }
}

/// <summary>Base class for dropdown menu sub-components.</summary>
public abstract class DropdownMenuSubComponent : StatelessComponent, IDropdownMenuChild
{
    public Core.Theme.IDropdownMenuTheme? MenuTheme { get; set; }
}

/// <summary>
/// A clickable item in the dropdown menu.
/// </summary>
public class DropdownMenuItem : DropdownMenuSubComponent
{
    /// <summary>Item text.</summary>
    public string? Text { get; set; }

    /// <summary>Keyboard shortcut hint displayed on the right.</summary>
    public string? Shortcut { get; set; }

    /// <summary>Whether this item is disabled.</summary>
    public bool Disabled { get; set; }

    /// <summary>Optional icon component displayed before the text.</summary>
    public IComponent? Icon { get; set; }

    /// <summary>Whether this item marks a destructive action (red styling).</summary>
    public bool Destructive { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var itemClass = MenuTheme?.Item ?? "eq-dropdown-item";
        var shortcutClass = MenuTheme?.Shortcut ?? "eq-dropdown-shortcut";
        var destructiveClass = Destructive ? "eq-dropdown-destructive" : "";

        var attrs = new Dictionary<string, string>
        {
            ["class"] = $"{itemClass} {destructiveClass} {ClassName}".Trim(),
            ["role"] = "menuitem",
            ["tabindex"] = "-1"
        };
        if (Disabled)
        {
            attrs["data-disabled"] = "true";
            attrs["aria-disabled"] = "true";
        }

        var element = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = attrs
        };

        if (Icon != null) element.Children.Add(Icon);

        if (Children.Any())
        {
            foreach (var child in Children) element.Children.Add(child);
        }
        else if (Text != null)
        {
            element.Children.Add(new Text(Text));
        }

        if (!string.IsNullOrEmpty(Shortcut))
        {
            element.Children.Add(new DynamicElement
            {
                TagName = "span",
                CustomAttributes = new Dictionary<string, string>
                {
                    ["class"] = shortcutClass
                },
                Children = { new Text(Shortcut) }
            });
        }

        return element;
    }
}

/// <summary>A visual separator between menu items.</summary>
public class DropdownMenuSeparator : DropdownMenuSubComponent
{
    public override IComponent Build(RenderContext context)
    {
        var separatorClass = MenuTheme?.Separator ?? "eq-dropdown-separator";

        return new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = separatorClass,
                ["role"] = "separator"
            }
        };
    }
}

/// <summary>A non-interactive label/heading within the menu.</summary>
public class DropdownMenuLabel : DropdownMenuSubComponent
{
    /// <summary>Label text.</summary>
    public string? Text { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var labelClass = MenuTheme?.Label ?? "eq-dropdown-label";

        var element = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = labelClass
            }
        };

        if (Children.Any())
            foreach (var child in Children) element.Children.Add(child);
        else if (Text != null)
            element.Children.Add(new Text(Text));

        return element;
    }
}

/// <summary>A logical group of menu items.</summary>
public class DropdownMenuGroup : DropdownMenuSubComponent
{
    public override IComponent Build(RenderContext context)
    {
        var element = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = "eq-dropdown-group",
                ["role"] = "group"
            }
        };

        foreach (var child in Children)
        {
            if (child is IDropdownMenuChild menuChild)
                menuChild.MenuTheme = MenuTheme;
            element.Children.Add(child);
        }

        return element;
    }
}
