using System.Text;

namespace eQuantic.UI.Web;

/// <summary>
/// The text of an SVG tree a tile carries in a data URL (#1937), written one way, so the server and
/// the runtime's twin (<c>svgMarkup</c> in lowering.ts) write the same bytes: attributes single-quoted
/// in the order they were set, <c>&amp;</c>, <c>'</c> and <c>&lt;</c> as entities, an element with no
/// child closed on itself, and no whitespace between elements.
/// <para>
/// Not <see cref="HtmlRenderer"/>: that writes an HTML document, double-quoted and with the escaping
/// HTML needs, and a data URL is an XML document of its own inside a CSS string.
/// </para>
/// </summary>
internal sealed class SvgMarkup
{
    private readonly StringBuilder _text = new();

    /// <summary>The markup of <paramref name="root"/> and everything under it.</summary>
    public static string Of(RealizedElement root)
    {
        var markup = new SvgMarkup();
        markup.Write(root);
        return markup._text.ToString();
    }

    private void Write(RealizedElement element)
    {
        _text.Append('<').Append(element.Tag);
        if (element.RawAttributes is { } attributes)
            foreach (var (name, value) in attributes)
            {
                _text.Append(' ').Append(name).Append("='");
                Escape(value);
                _text.Append('\'');
            }
        if (element.Children.Count == 0)
        {
            _text.Append("/>");
            return;
        }
        _text.Append('>');
        foreach (var child in element.Children)
            if (child is RealizedElement inner) Write(inner);
        _text.Append("</").Append(element.Tag).Append('>');
    }

    private void Escape(string value)
    {
        foreach (var c in value)
            _text.Append(c switch
            {
                '&' => "&amp;",
                '\'' => "&apos;",
                '<' => "&lt;",
                _ => c.ToString(),
            });
    }
}
