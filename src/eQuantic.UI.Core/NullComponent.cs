namespace eQuantic.UI.Core;

public class NullComponent : HtmlElement
{
    public override HtmlNode Render()
    {
        // Render nothing (empty node or comment)
        return new HtmlNode { Tag = "#comment", Attributes = { { "text", "null" } } };
    }
}
