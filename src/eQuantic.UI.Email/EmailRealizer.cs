using System.Text;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Email;

/// <summary>
/// Lowers the shared abstract components to the HTML an EMAIL CLIENT will actually render.
/// <para>
/// A third realizer beside web and Photon, for a target whose engine is merely very restrictive:
/// Outlook on Windows renders with Word's engine, so there is no flexbox, no grid, no <c>gap</c>
/// and no stylesheet — layout is nested tables and every style is inline. Colors are LITERAL
/// (email renders in one mode; the theme's light leg is that mode), and the theme's type ramp is
/// inlined onto every text, because the web's <c>eq-type-*</c> classes reference a stylesheet an
/// email does not have.
/// </para>
/// <para>
/// Anything this medium cannot carry fails LOUD (<see cref="NotSupportedException"/> naming the
/// node) — the repo's rule: never a silent divergence. An email is a printed page that happens to
/// have links; scrolling, pressing and dragging are not smaller here, they are nothing.
/// </para>
/// </summary>
public static class EmailRealizer
{
    /// <summary>The lowered tree as an HTML fragment — tables, inline styles, literal colors.</summary>
    public static string Lower(VisualNode node, IAppTheme theme)
    {
        var html = new StringBuilder();
        Write(node, theme, html);
        return html.ToString();
    }

    private static void Write(VisualNode node, IAppTheme theme, StringBuilder html)
    {
        switch (node)
        {
            case Column column:
                WriteColumn(column, theme, html);
                break;
            case Row row:
                WriteRow(row, theme, html);
                break;
            case Text text:
                WriteText(text, theme, html);
                break;
            case Box box:
                WriteBox(box, theme, html);
                break;
            case Image image:
                WriteImage(image, html);
                break;
            case Link link:
                WriteLink(link, theme, html);
                break;
            default:
                // Fail loud with the node's name and the reason, because the alternative is a page
                // that renders without a piece of itself and says nothing.
                throw new NotSupportedException(
                    $"A {node.GetType().Name} cannot be realized in an email: the medium has no "
                    + "scrolling, no interaction and no script. Compose the message from Column, "
                    + "Row, Text, Box and Image.");
        }
    }

    /// <summary>
    /// A Column is a table with one row per child. Its gap — which no email engine implements as a
    /// property — is a SPACER ROW with an explicit height, between children and never after the
    /// last, which is exactly what <c>gap</c> means.
    /// </summary>
    private static void WriteColumn(Column column, IAppTheme theme, StringBuilder html)
    {
        html.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"border-collapse: collapse\">");
        var first = true;
        foreach (var child in column.Children)
        {
            if (!first && column.Gap > 0)
                html.Append($"<tr><td style=\"height: {(int)column.Gap}px; line-height: {(int)column.Gap}px; font-size: 0\">&nbsp;</td></tr>");
            first = false;

            html.Append("<tr><td style=\"vertical-align: top\">");
            Write(child, theme, html);
            html.Append("</td></tr>");
        }
        html.Append("</table>");
    }

    /// <summary>A Row is exactly one table row; its gap is a spacer CELL with an explicit width.</summary>
    private static void WriteRow(Row row, IAppTheme theme, StringBuilder html)
    {
        html.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"border-collapse: collapse\"><tr>");
        var first = true;
        foreach (var child in row.Children)
        {
            if (!first && row.Gap > 0)
                html.Append($"<td style=\"width: {(int)row.Gap}px; font-size: 0\">&nbsp;</td>");
            first = false;

            html.Append("<td style=\"vertical-align: middle\">");
            Write(child, theme, html);
            html.Append("</td>");
        }
        html.Append("</tr></table>");
    }

    /// <summary>
    /// A Text carries the theme's ramp INLINE — size, line-height, weight, and its ink as one
    /// literal color. The email renders in one mode, and the theme's light leg is that mode.
    /// </summary>
    private static void WriteText(Text text, IAppTheme theme, StringBuilder html)
    {
        var style = text.StyleOverride ?? theme.Type(text.Role);
        var ink = (text.Color ?? theme.TextPrimary).Resolve(ThemeMode.Light);

        html.Append("<span style=\"")
            .Append($"font-size: {(int)style.Size}px; ")
            .Append($"line-height: {(int)style.LineHeight}px; ")
            .Append($"font-weight: {(int)style.Weight}; ")
            .Append("font-family: -apple-system, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; ")
            .Append($"color: {Hex(ink)}\">")
            .Append(Escape(text.PlainContent))
            .Append("</span>");
    }

    /// <summary>A Box paints a background and padding on its cell — the two things a Box means here.</summary>
    private static void WriteBox(Box box, IAppTheme theme, StringBuilder html)
    {
        var style = new StringBuilder("border-collapse: collapse");
        var cell = new StringBuilder("vertical-align: top");

        if (box.Style.Background is { } background)
            cell.Append($"; background-color: {Hex(background.Resolve(ThemeMode.Light))}");
        // Start/End resolve as left/right: an email body is LTR unless the document says otherwise,
        // and direction-aware layout is beyond what this medium can promise.
        if (box.Style.Padding is { } padding)
            cell.Append("; padding: ").Append(
                padding.Top == padding.End && padding.End == padding.Bottom && padding.Bottom == padding.Start
                    ? $"{(int)padding.Top}px"
                    : $"{(int)padding.Top}px {(int)padding.End}px {(int)padding.Bottom}px {(int)padding.Start}px");
        // A radius is honoured by the clients that can (Apple Mail, Gmail) and squarely ignored by
        // Word's engine — a degradation, not a divergence: the box is the same box with corners.
        if (box.Style.CornerRadius is { } radius && radius != CornerRadii.Zero)
            cell.Append($"; border-radius: {(int)radius.TopLeft}px");

        html.Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"{style}\">")
            .Append($"<tr><td style=\"{cell}\">");
        if (box.Child is { } child) Write(child, theme, html);
        html.Append("</td></tr></table>");
    }

    /// <summary>
    /// An image by ABSOLUTE address, sized by ATTRIBUTES. The reader's client opens the message
    /// with no origin to resolve a relative URL against, and Gmail strips <c>data:</c> URIs — so
    /// either one is an error here, not a broken picture in someone's inbox. Word's engine sizes
    /// images from the width/height attributes, and <c>display: block</c> kills the baseline gap
    /// every client adds under an inline image.
    /// </summary>
    private static void WriteImage(Image image, StringBuilder html)
    {
        RequireAbsolute(image.Source, "An image in an email");

        // One mode, decided once: colors take the theme's light leg, artwork takes the light source.
        html.Append($"<img src=\"{image.Source}\" width=\"{(int)image.Width}\" height=\"{(int)image.Height}\" ")
            .Append($"alt=\"{Escape(image.Alt)}\" ")
            .Append($"style=\"display: block; border: 0; width: {(int)image.Width}px; height: {(int)image.Height}px");
        if (image.CornerRadius != CornerRadii.Zero)
            html.Append($"; border-radius: {(int)image.CornerRadius.TopLeft}px");
        html.Append("\">");
    }

    /// <summary>
    /// A Link is a destination and a child; the child says what it looks like. A TEXT child is
    /// underlined — there is no hover in this medium to reveal a link, so it must look like one. A
    /// painted child (the bulletproof-button pattern: a Box around a label) is not — the box is the
    /// affordance — and the anchor is inline-block so it takes the box's size.
    /// </summary>
    private static void WriteLink(Link link, IAppTheme theme, StringBuilder html)
    {
        RequireAbsolute(link.Destination, "A link in an email");

        var textual = link.Child is Text;
        html.Append($"<a href=\"{link.Destination}\" ")
            .Append(textual
                ? "style=\"text-decoration: underline; color: inherit\">"
                : "style=\"text-decoration: none; color: inherit; display: inline-block\">");
        Write(link.Child, theme, html);
        html.Append("</a>");
    }

    /// <summary>Absolute https/http or mailto — the only addresses that mean anything in an inbox.</summary>
    private static void RequireAbsolute(string address, string what)
    {
        if (address.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                $"{what} cannot use a data: URI — Gmail strips them. Serve the asset from an "
                + "absolute https URL.");
        if (!address.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !address.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !address.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                $"{what} needs an absolute address ('{address}' is relative): the reader's client "
                + "opens the message with no origin to resolve it against.");
    }

    internal static string Hex(Color color) =>
        color.A == 255
            ? $"#{color.R:x2}{color.G:x2}{color.B:x2}"
            : $"#{color.R:x2}{color.G:x2}{color.B:x2}{color.A:x2}";

    internal static string Escape(string text) => text
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
