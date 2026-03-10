using System.Collections.Generic;
using System.Linq;

namespace eQuantic.UI.Core;

/// <summary>
/// Primitive element for SVG rendering.
/// Can represent any SVG tag based on the 'As' property (e.g., svg, path, rect, circle, etc.).
/// </summary>
public class SvgElement : HtmlElement
{
    /// <summary>
    /// The SVG tag to render. Defaults to 'svg'.
    /// </summary>
    public string As { get; set; } = "svg";

    #region SVG Specific Attributes

    public string? Xmlns { get; set; } = "http://www.w3.org/2000/svg";
    public string? ViewBox { get; set; }
    public string? Stroke { get; set; }
    public string? StrokeWidth { get; set; }
    public string? StrokeLinecap { get; set; }
    public string? StrokeLinejoin { get; set; }
    public string? Fill { get; set; }
    public string? D { get; set; }
    public string? Points { get; set; }
    public string? X1 { get; set; }
    public string? Y1 { get; set; }
    public string? X2 { get; set; }
    public string? Y2 { get; set; }
    public string? Cx { get; set; }
    public string? Cy { get; set; }
    public string? R { get; set; }
    public string? X { get; set; }
    public string? Y { get; set; }

    #endregion

    /// <inheritdoc />
    public override HtmlNode Render()
    {
        var attributes = BuildAttributes();
        
        // Map SVG-specific properties to attributes
        if (As == "svg" && !string.IsNullOrEmpty(Xmlns)) attributes["xmlns"] = Xmlns;
        if (!string.IsNullOrEmpty(ViewBox)) attributes["viewBox"] = ViewBox;
        if (!string.IsNullOrEmpty(Stroke)) attributes["stroke"] = Stroke;
        if (!string.IsNullOrEmpty(StrokeWidth)) attributes["stroke-width"] = StrokeWidth;
        if (!string.IsNullOrEmpty(StrokeLinecap)) attributes["stroke-linecap"] = StrokeLinecap;
        if (!string.IsNullOrEmpty(StrokeLinejoin)) attributes["stroke-linejoin"] = StrokeLinejoin;
        if (!string.IsNullOrEmpty(Fill)) attributes["fill"] = Fill;
        if (!string.IsNullOrEmpty(D)) attributes["d"] = D;
        if (!string.IsNullOrEmpty(Points)) attributes["points"] = Points;
        if (!string.IsNullOrEmpty(X1)) attributes["x1"] = X1;
        if (!string.IsNullOrEmpty(Y1)) attributes["y1"] = Y1;
        if (!string.IsNullOrEmpty(X2)) attributes["x2"] = X2;
        if (!string.IsNullOrEmpty(Y2)) attributes["y2"] = Y2;
        if (!string.IsNullOrEmpty(Cx)) attributes["cx"] = Cx;
        if (!string.IsNullOrEmpty(Cy)) attributes["cy"] = Cy;
        if (!string.IsNullOrEmpty(R)) attributes["r"] = R;
        if (!string.IsNullOrEmpty(X)) attributes["x"] = X;
        if (!string.IsNullOrEmpty(Y)) attributes["y"] = Y;

        return new HtmlNode
        {
            Tag = As,
            Attributes = attributes,
            Events = BuildEvents(),
            Children = Children.Select(c => c.Render()).ToList()
        };
    }
}
