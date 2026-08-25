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

    internal static string Hex(Color color) =>
        color.A == 255
            ? $"#{color.R:x2}{color.G:x2}{color.B:x2}"
            : $"#{color.R:x2}{color.G:x2}{color.B:x2}{color.A:x2}";

    internal static string Escape(string text) => text
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
