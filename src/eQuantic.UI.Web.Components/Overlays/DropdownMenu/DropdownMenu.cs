using System;
using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Web.Components.Overlays.DropdownMenu;

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

        var container = new Box
        {
            As = "div",
            ClassName = $"eq-dropdown {ClassName}".Trim(),
            DataAttributes = new Dictionary<string, string> { ["state"] = IsOpen ? "open" : "closed" }
        };

        // Trigger
        if (Trigger != null)
        {
            var triggerWrapper = new Box
            {
                As = "button",
                ClassName = "eq-dropdown-trigger",
                Type = "button",
                AriaHasPopup = "menu",
                AriaExpanded = IsOpen,
                DataAttributes = new Dictionary<string, string> { ["state"] = IsOpen ? "open" : "closed" }
            };
            triggerWrapper.Children.Add(Trigger);
            container.Children.Add(triggerWrapper);
        }

        // Content
        if (IsOpen)
        {
            var content = new Box
            {
                As = "div",
                ClassName = contentClass,
                Role = "menu",
                AriaOrientation = "vertical",
                TabIndex = -1,
                DataAttributes = new Dictionary<string, string>
                {
                    ["state"] = "open",
                    ["side"] = Side.ToString().ToLowerInvariant(),
                    ["align"] = Align.ToString().ToLowerInvariant()
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

        var element = new Box
        {
            As = "div",
            ClassName = $"{itemClass} {destructiveClass} {ClassName}".Trim(),
            Role = "menuitem",
            TabIndex = -1
        };
        if (Disabled)
        {
            element.DataAttributes = new Dictionary<string, string> { ["disabled"] = "true" };
            element.AriaDisabled = true;
        }

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
            element.Children.Add(new Box
            {
                As = "span",
                ClassName = shortcutClass,
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

        return new Box
        {
            As = "div",
            ClassName = separatorClass,
            Role = "separator"
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

        var element = new Box
        {
            As = "div",
            ClassName = labelClass
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
        var element = new Box
        {
            As = "div",
            ClassName = "eq-dropdown-group",
            Role = "group"
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
