using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Feedback;

public class Modal : HtmlElement
{
    public new string? Title { get; set; }
    public HtmlElement? Header { get; set; }
    public HtmlElement? Body { get; set; }
    public HtmlElement? Footer { get; set; }
    public bool IsOpen { get; set; }
    public bool CloseOnBackdrop { get; set; } = true;
    public Action? OnClose { get; set; }

    public override HtmlNode Render()
    {
        if (!IsOpen)
        {
            return new HtmlNode { Tag = "template" }; // Empty placeholder when closed
        }

        var attrs = BuildAttributes();
        attrs["class"] = (attrs.GetValueOrDefault("class") + " modal fade show").Trim();
        attrs["style"] = "display: block;";
        attrs["tabindex"] = "-1";
        attrs["role"] = "dialog";

        var modalContent = new List<HtmlNode>();

        // Header
        if (Header != null || Title != null)
        {
            var headerChildren = new List<HtmlNode>();
            
            if (Title != null)
            {
                headerChildren.Add(new HtmlNode
                {
                    Tag = "h5",
                    Attributes = new Dictionary<string, string?> { ["class"] = "modal-title" },
                    Children = { HtmlNode.Text(Title) }
                });
            }
            else if (Header != null)
            {
                headerChildren.Add(Header.Render());
            }

            headerChildren.Add(new HtmlNode
            {
                Tag = "button",
                Attributes = new Dictionary<string, string?>
                {
                    ["type"] = "button",
                    ["class"] = "btn-close",
                    ["aria-label"] = "Close"
                }
            });

            modalContent.Add(new HtmlNode
            {
                Tag = "div",
                Attributes = new Dictionary<string, string?> { ["class"] = "modal-header" },
                Children = headerChildren
            });
        }

        // Body
        if (Body != null)
        {
            modalContent.Add(new HtmlNode
            {
                Tag = "div",
                Attributes = new Dictionary<string, string?> { ["class"] = "modal-body" },
                Children = { Body.Render() }
            });
        }
        else if (Children.Any())
        {
            modalContent.Add(new HtmlNode
            {
                Tag = "div",
                Attributes = new Dictionary<string, string?> { ["class"] = "modal-body" },
                Children = Children.Select(c => c.Render()).ToList()
            });
        }

        // Footer
        if (Footer != null)
        {
            modalContent.Add(new HtmlNode
            {
                Tag = "div",
                Attributes = new Dictionary<string, string?> { ["class"] = "modal-footer" },
                Children = { Footer.Render() }
            });
        }

        var dialog = new HtmlNode
        {
            Tag = "div",
            Attributes = new Dictionary<string, string?> { ["class"] = "modal-dialog" },
            Children = {
                new HtmlNode
                {
                    Tag = "div",
                    Attributes = new Dictionary<string, string?> { ["class"] = "modal-content" },
                    Children = modalContent
                }
            }
        };

        // Backdrop
        var backdrop = new HtmlNode
        {
            Tag = "div",
            Attributes = new Dictionary<string, string?> { ["class"] = "modal-backdrop fade show" }
        };

        // Wrapper containing modal + backdrop
        return new HtmlNode
        {
            Tag = "div",
            Children = {
                new HtmlNode
                {
                    Tag = "div",
                    Attributes = attrs,
                    Children = { dialog }
                },
                backdrop
            }
        };
    }
}
