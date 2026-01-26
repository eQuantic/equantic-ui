using System;
using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Components.Feedback;

public enum ToastPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

public class Toast : HtmlElement
{
    public new string? Title { get; set; }
    public string? Message { get; set; }
    public Variant Variant { get; set; } = Variant.Info;
    public bool AutoHide { get; set; } = true;
    public int DelayMs { get; set; } = 5000;
    public Action? OnClose { get; set; }

    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        attrs["class"] = (attrs.GetValueOrDefault("class") + " toast show").Trim();
        attrs["role"] = "alert";
        attrs["aria-live"] = "assertive";
        attrs["aria-atomic"] = "true";

        var children = new List<HtmlNode>();

        if (Title != null)
        {
            children.Add(new HtmlNode
            {
                Tag = "div",
                Attributes = new Dictionary<string, string?> 
                { 
                    ["class"] = $"toast-header bg-{Variant.ToString().ToLowerInvariant()} text-white" 
                },
                Children = {
                    new HtmlNode
                    {
                        Tag = "strong",
                        Attributes = new Dictionary<string, string?> { ["class"] = "me-auto" },
                        Children = { HtmlNode.Text(Title) }
                    },
                    new HtmlNode
                    {
                        Tag = "button",
                        Attributes = new Dictionary<string, string?>
                        {
                            ["type"] = "button",
                            ["class"] = "btn-close btn-close-white",
                            ["aria-label"] = "Close"
                        }
                    }
                }
            });
        }

        if (Message != null)
        {
            children.Add(new HtmlNode
            {
                Tag = "div",
                Attributes = new Dictionary<string, string?> { ["class"] = "toast-body" },
                Children = { HtmlNode.Text(Message) }
            });
        }

        children.AddRange(Children.Select(c => c.Render()));

        return new HtmlNode
        {
            Tag = "div",
            Attributes = attrs,
            Children = children
        };
    }
}

/// <summary>
/// Container for positioning toasts
/// </summary>
public class ToastContainer : HtmlElement
{
    public ToastPosition Position { get; set; } = ToastPosition.TopRight;

    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        var positionClass = Position switch
        {
            ToastPosition.TopLeft => "top-0 start-0",
            ToastPosition.TopCenter => "top-0 start-50 translate-middle-x",
            ToastPosition.TopRight => "top-0 end-0",
            ToastPosition.BottomLeft => "bottom-0 start-0",
            ToastPosition.BottomCenter => "bottom-0 start-50 translate-middle-x",
            ToastPosition.BottomRight => "bottom-0 end-0",
            _ => "top-0 end-0"
        };

        attrs["class"] = $"toast-container position-fixed p-3 {positionClass}";

        return new HtmlNode
        {
            Tag = "div",
            Attributes = attrs,
            Children = Children.Select(c => c.Render()).ToList()
        };
    }
}
