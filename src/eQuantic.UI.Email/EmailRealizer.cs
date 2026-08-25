using System.Globalization;
using System.Text;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Email;

/// <summary>
/// Lowers the shared abstract components to the HTML an EMAIL CLIENT will actually render.
/// <para>
/// A third realizer beside web and Photon, for a target whose engine is merely very restrictive:
/// Outlook on Windows renders with Word's engine, so there is no flexbox, no grid, no <c>gap</c>
/// and no stylesheet — layout is nested tables and every style is inline. Colors are LITERAL
/// (email renders in one mode; the theme's light leg is that mode, and a translucent token is
/// flattened over the theme's surface, because Word does not read 8-digit hex), and the theme's
/// type ramp is inlined onto every text.
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
        Write(node, new ComponentContext(theme), html);
        return html.ToString();
    }

    internal static void Write(VisualNode node, ComponentContext context, StringBuilder html)
    {
        switch (node)
        {
            case Column column:
                WriteColumn(column, context, html);
                break;
            case Row row:
                WriteRow(row, context, html);
                break;
            case Text text:
                WriteText(text, context.Theme, html);
                break;
            case Box box:
                WriteBox(box, context, html);
                break;
            case Image image:
                WriteImage(image, html);
                break;
            case Link link:
                WriteLink(link, context, html);
                break;
            case UiComponent component:
                // A shared component nested anywhere in the tree expands here, exactly as it does
                // on the web — BuildContained, so one component's throw costs that component's
                // subtree and not the whole message.
                Write(component.BuildContained(context), context, html);
                break;
            default:
                // Fail loud with the node's name and the reason, because the alternative is a page
                // that renders without a piece of itself and says nothing.
                throw new NotSupportedException(
                    $"A {node.GetType().Name} cannot be realized in an email: the medium has no "
                    + "scrolling, no interaction and no script. Compose the message from Column, "
                    + "Row, Text, Box, Image and Link.");
        }
    }

    /// <summary>
    /// A Column is a table with one row per child. Its gap — which no email engine implements as a
    /// property — is a SPACER ROW with an explicit height, between children and never after the
    /// last, which is exactly what <c>gap</c> means.
    /// </summary>
    private static void WriteColumn(Column column, ComponentContext context, StringBuilder html)
    {
        html.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"border-collapse: collapse\">");
        var first = true;
        foreach (var child in column.Children)
        {
            if (!first && column.Gap > 0)
                html.Append($"<tr><td style=\"height: {Px(column.Gap)}; line-height: {Px(column.Gap)}; font-size: 0\">&nbsp;</td></tr>");
            first = false;

            html.Append("<tr><td style=\"vertical-align: top\">");
            Write(child, context, html);
            html.Append("</td></tr>");
        }
        html.Append("</table>");
    }

    /// <summary>A Row is exactly one table row; its gap is a spacer CELL with an explicit width.</summary>
    private static void WriteRow(Row row, ComponentContext context, StringBuilder html)
    {
        html.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"border-collapse: collapse\"><tr>");
        var first = true;
        foreach (var child in row.Children)
        {
            if (!first && row.Gap > 0)
                html.Append($"<td style=\"width: {Px(row.Gap)}; font-size: 0\">&nbsp;</td>");
            first = false;

            html.Append("<td style=\"vertical-align: middle\">");
            Write(child, context, html);
            html.Append("</td>");
        }
        html.Append("</tr></table>");
    }

    /// <summary>
    /// A Text carries the theme's ramp INLINE — size, line-height, weight, tracking, slant and
    /// face — and its ink as one literal color. Runs render as nested inline elements so a bold
    /// word, a colored fragment or a LINKED run survives into the mail; flattening them was a
    /// silent loss, and a linked run in particular became text the reader could not act on.
    /// </summary>
    private static void WriteText(Text text, IAppTheme theme, StringBuilder html)
    {
        var style = text.StyleOverride ?? theme.Type(text.Role);
        var ink = Literal(text.Color ?? theme.TextPrimary, theme);

        // text-align applies to blocks, so an aligned paragraph gets a block wrapper; a start-
        // aligned one (the default) stays inline and composes inside Rows and Links unchanged.
        var aligned = text.Align != TextAlignment.Start;
        if (aligned)
            html.Append($"<div style=\"text-align: {(text.Align == TextAlignment.Center ? "center" : "right")}\">");

        html.Append("<span style=\"")
            .Append($"font-size: {Px(style.Size)}; ")
            .Append($"line-height: {Px(style.LineHeight)}; ")
            .Append($"font-weight: {(int)style.Weight}; ");
        if (style.Tracking != 0)
            html.Append($"letter-spacing: {Px(style.Tracking)}; ");
        if (style.Italic)
            html.Append("font-style: italic; ");
        if (text.Tabular)
            html.Append("font-variant-numeric: tabular-nums; ");
        html.Append($"font-family: {Family(text.Mono || style.Mono)}; ")
            .Append($"color: {ink}\">");

        if (text.Spans is { Count: > 0 } spans)
            foreach (var run in spans) WriteRun(run, theme, html);
        else
            html.Append(Escape(text.Content));

        html.Append("</span>");
        if (aligned) html.Append("</div>");
    }

    /// <summary>One run: only what it OVERRIDES is declared, and a destination makes it an anchor.</summary>
    private static void WriteRun(TextRun run, IAppTheme theme, StringBuilder html)
    {
        var overrides = new StringBuilder();
        if (run.Color is { } color) overrides.Append($"color: {Literal(color, theme)}; ");
        if (run.Weight is { } weight) overrides.Append($"font-weight: {(int)weight}; ");
        if (run.Italic) overrides.Append("font-style: italic; ");
        if (run.Mono) overrides.Append($"font-family: {Family(mono: true)}; ");

        var tag = run.Destination is not null ? "a" : overrides.Length > 0 ? "span" : null;
        if (tag is null)
        {
            html.Append(Escape(run.Content));
            return;
        }

        html.Append('<').Append(tag);
        if (run.Destination is { } destination)
        {
            RequireAbsolute(destination, "A linked run in an email", allowMailto: true);
            // A linked run is body text: it must look like a link (underline — no hover exists
            // here) but keep the paragraph's ink unless the run says otherwise.
            html.Append($" href=\"{EscapeAttribute(destination)}\"");
            overrides.Append("text-decoration: underline; ");
            if (run.Color is null) overrides.Append("color: inherit; ");
        }
        if (overrides.Length > 0)
            html.Append($" style=\"{overrides.ToString().TrimEnd(' ', ';')}\"");
        html.Append('>').Append(Escape(run.Content)).Append($"</{tag}>");
    }

    /// <summary>A Box paints a background and padding on its cell — the two things a Box means here.</summary>
    private static void WriteBox(Box box, ComponentContext context, StringBuilder html)
    {
        var cell = new StringBuilder("vertical-align: top");

        if (box.Style.Background is { } background)
            cell.Append($"; background-color: {Literal(background, context.Theme)}");
        // Start/End resolve as left/right: an email body is LTR unless the document says otherwise,
        // and direction-aware layout is beyond what this medium can promise.
        if (box.Style.Padding is { } padding)
            cell.Append("; padding: ").Append(
                padding.Top == padding.End && padding.End == padding.Bottom && padding.Bottom == padding.Start
                    ? Px(padding.Top)
                    : $"{Px(padding.Top)} {Px(padding.End)} {Px(padding.Bottom)} {Px(padding.Start)}");
        // A radius is honoured by the clients that can (Apple Mail, Gmail) and squarely ignored by
        // Word's engine — a degradation, not a divergence: the box is the same box with corners.
        if (box.Style.CornerRadius is { } radius && radius != CornerRadii.Zero)
            cell.Append($"; border-radius: {Px(radius.TopLeft)}");

        html.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"border-collapse: collapse\">")
            .Append($"<tr><td style=\"{cell}\">");
        if (box.Child is { } child) Write(child, context, html);
        html.Append("</td></tr></table>");
    }

    /// <summary>
    /// An image by ABSOLUTE http(s) address, sized by ATTRIBUTES. The reader's client opens the
    /// message with no origin to resolve a relative URL against, and Gmail strips <c>data:</c>
    /// URIs — so either one is an error here, not a broken picture in someone's inbox. Word's
    /// engine sizes images from the width/height attributes, and <c>display: block</c> kills the
    /// baseline gap every client adds under an inline image.
    /// <para>
    /// FIT, in a medium with no <c>object-fit</c> and no cropping: <see cref="ImageFit.Contain"/>
    /// scales by width with the height left auto (whole source visible, the box's height follows
    /// the image); <see cref="ImageFit.Cover"/> and <see cref="ImageFit.Stretch"/> pin both
    /// dimensions — identical output, and for Cover that carries a CONTRACT: email cannot crop, so
    /// the asset must be served at the box's aspect ratio, or it will distort exactly as Stretch
    /// promises to.
    /// </para>
    /// </summary>
    private static void WriteImage(Image image, StringBuilder html)
    {
        RequireAbsolute(image.Source, "An image in an email", allowMailto: false);

        // One mode, decided once: colors take the theme's light leg, artwork takes the light source.
        html.Append($"<img src=\"{EscapeAttribute(image.Source)}\" width=\"{(int)image.Width}\" ");
        if (image.Fit != ImageFit.Contain)
            html.Append($"height=\"{(int)image.Height}\" ");
        html.Append($"alt=\"{EscapeAttribute(image.Alt)}\" ")
            .Append($"style=\"display: block; border: 0; width: {Px(image.Width)}; ")
            .Append(image.Fit == ImageFit.Contain ? "height: auto" : $"height: {Px(image.Height)}");
        if (image.CornerRadius != CornerRadii.Zero)
            html.Append($"; border-radius: {Px(image.CornerRadius.TopLeft)}");
        html.Append("\">");
    }

    /// <summary>
    /// A Link is a destination and a child; the child says what it looks like. A TEXT child is
    /// underlined — there is no hover in this medium to reveal a link, so it must look like one. A
    /// painted child (the bulletproof-button pattern: a Box around a label) is not — the box is the
    /// affordance — and the anchor is inline-block so it takes the box's size.
    /// </summary>
    private static void WriteLink(Link link, ComponentContext context, StringBuilder html)
    {
        RequireAbsolute(link.Destination, "A link in an email", allowMailto: true);

        var textual = link.Child is Text;
        html.Append($"<a href=\"{EscapeAttribute(link.Destination)}\" ")
            .Append(textual
                ? "style=\"text-decoration: underline; color: inherit\">"
                : "style=\"text-decoration: none; color: inherit; display: inline-block\">");
        Write(link.Child, context, html);
        html.Append("</a>");
    }

    /// <summary>
    /// Absolute http(s) — the only address that means anything in an inbox — plus <c>mailto:</c>
    /// where a LINK is being validated: a mailto image source would be a guaranteed broken picture.
    /// </summary>
    private static void RequireAbsolute(string address, string what, bool allowMailto)
    {
        if (address.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                $"{what} cannot use a data: URI — Gmail strips them. Serve the asset from an "
                + "absolute https URL.");
        if (allowMailto && address.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            return;
        if (!address.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !address.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                $"{what} needs an absolute address ('{address}' is not): the reader's client "
                + "opens the message with no origin to resolve it against.");
    }

    /// <summary>
    /// A token as ONE literal color. Email renders in one mode — the light leg — and a translucent
    /// token is FLATTENED over the theme's surface, because the 8-digit hex a raw alpha would need
    /// is CSS Color 4, which Word's engine (and older clients) do not read: the color would not be
    /// dimmed there, it would be lost.
    /// </summary>
    internal static string Literal(ColorToken token, IAppTheme theme)
    {
        var color = token.Resolve(ThemeMode.Light);
        if (color.A == 255) return Hex(color);

        var surface = theme.Surface.Resolve(ThemeMode.Light);
        var alpha = color.A / 255f;
        return Hex(Color.FromRgb(
            (byte)float.Round(color.R * alpha + surface.R * (1 - alpha)),
            (byte)float.Round(color.G * alpha + surface.G * (1 - alpha)),
            (byte)float.Round(color.B * alpha + surface.B * (1 - alpha))));
    }

    internal static string Hex(Color color) => $"#{color.R:x2}{color.G:x2}{color.B:x2}";

    /// <summary>A CSS length, invariant and fraction-preserving: 16 → "16px", 13.5 → "13.5px".</summary>
    internal static string Px(float value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture) + "px";

    private static string Family(bool mono) => mono
        ? "ui-monospace, 'Cascadia Mono', 'Segoe UI Mono', Menlo, Consolas, monospace"
        : "-apple-system, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif";

    internal static string Escape(string text) => text
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>
    /// Attribute values additionally escape the QUOTE: an alt or URL containing one would end the
    /// attribute early and inject markup into the mail — and a plain query-string <c>&amp;</c>,
    /// unescaped, is malformed HTML some clients then re-write.
    /// </summary>
    internal static string EscapeAttribute(string value) => Escape(value).Replace("\"", "&quot;");
}
