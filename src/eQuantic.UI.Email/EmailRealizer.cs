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
                // A shared component expands here — via Build, NOT the web's BuildContained, and
                // the divergence is deliberate: BuildContained catches a component's throw and
                // renders a describe-box in its place, which is right on a live page a developer
                // is looking at and wrong in a message about to be SENT to a reader. In email, a
                // broken component must fail the send, never reach an inbox dressed as content.
                Write(component.Build(context), context, html);
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
    /// <summary>
    /// MainAlign is VACUOUS here, on both axes, and that is documented rather than fenced: an email
    /// table sizes to its content, so there is no free space for Start/Center/End to distribute and
    /// no gap for SpaceBetween to widen — every value renders identically, which is faithful, not
    /// divergent. Cross is real (a narrower child sits somewhere in a full-width column) and maps
    /// to the cell's align; padding wraps the table in the one-cell shell email understands.
    /// </summary>
    private static void WriteColumn(Column column, ComponentContext context, StringBuilder html)
    {
        OpenPadding(column.Padding, html);
        html.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"border-collapse: collapse\">");
        var first = true;
        foreach (var child in column.Children)
        {
            if (!first && column.Gap > 0)
                html.Append($"<tr><td style=\"height: {Px(column.Gap)}; line-height: {Px(column.Gap)}; font-size: 0\">&nbsp;</td></tr>");
            first = false;

            // Per CHILD, because AlignSelf overrides the column's cross for that child alone —
            // each child owns a cell here, so the medium expresses it for free.
            var align = (child.AlignSelf ?? column.Cross) switch
            {
                CrossAlign.Center => " align=\"center\"",
                CrossAlign.End => " align=\"right\"",
                _ => "",
            };
            html.Append($"<tr><td{align} style=\"vertical-align: top\">");
            Write(child, context, html);
            html.Append("</td></tr>");
        }
        html.Append("</table>");
        ClosePadding(column.Padding, html);
    }

    /// <summary>Container padding, in the one form every client honours: a one-cell wrapper table.</summary>
    private static void OpenPadding(EdgeInsets padding, StringBuilder html)
    {
        if (padding == EdgeInsets.Zero) return;
        var value = padding.Top == padding.End && padding.End == padding.Bottom && padding.Bottom == padding.Start
            ? Px(padding.Top)
            : $"{Px(padding.Top)} {Px(padding.End)} {Px(padding.Bottom)} {Px(padding.Start)}";
        html.Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"border-collapse: collapse\"><tr><td style=\"padding: {value}\">");
    }

    private static void ClosePadding(EdgeInsets padding, StringBuilder html)
    {
        if (padding != EdgeInsets.Zero) html.Append("</td></tr></table>");
    }

    /// <summary>A Row is exactly one table row; its gap is a spacer CELL with an explicit width.</summary>
    private static void WriteRow(Row row, ComponentContext context, StringBuilder html)
    {
        OpenPadding(row.Padding, html);
        html.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"border-collapse: collapse\"><tr>");
        var first = true;
        foreach (var child in row.Children)
        {
            if (!first && row.Gap > 0)
                html.Append($"<td style=\"width: {Px(row.Gap)}; font-size: 0\">&nbsp;</td>");
            first = false;

            html.Append($"<td style=\"vertical-align: {CrossOf(child.AlignSelf ?? row.Cross)}\">");
            Write(child, context, html);
            html.Append("</td>");
        }
        html.Append("</tr></table>");
        ClosePadding(row.Padding, html);
    }

    /// <summary>
    /// A Text carries the theme's ramp INLINE — size, line-height, weight, tracking, slant and
    /// face — and its ink as one literal color. Runs render as nested inline elements so a bold
    /// word, a colored fragment or a LINKED run survives into the mail; flattening them was a
    /// silent loss, and a linked run in particular became text the reader could not act on.
    /// </summary>
    private static void WriteText(Text text, IAppTheme theme, StringBuilder html)
    {
        // The two Text options this medium cannot express, fenced rather than approximated:
        // gradient ink (background-clip: text is not email CSS — a solid stand-in would be a
        // different ink nobody chose) and MaxLines (no client clamps lines, and showing MORE text
        // than the author bounded is a content divergence, not a style one).
        if (text.Gradient is not null)
            throw new NotSupportedException(
                "Gradient text cannot be realized in an email — no client paints it. Give the "
                + "email's text a solid color.");
        if (text.MaxLines > 0)
            throw new NotSupportedException(
                "MaxLines cannot be realized in an email — no client clamps lines, so the whole "
                + "text would show. Shorten the content for this medium instead.");

        var style = text.StyleOverride ?? theme.Type(text.Role);
        var ink = Literal(text.Color ?? theme.TextPrimary, theme);

        // text-align applies to blocks, so an aligned paragraph gets a block wrapper; a start-
        // aligned one (the default) stays inline and composes inside Rows and Links unchanged.
        var aligned = text.Align != TextAlignment.Start;
        if (aligned)
            html.Append($"<div style=\"text-align: {(text.Align == TextAlignment.Center ? "center" : "right")}\">");

        // A heading level keeps its ELEMENT — outline navigation in the clients and screen readers
        // that offer it — with margin zeroed so the tag is semantics, not layout.
        var tag = text.HeadingLevel is >= 1 and <= 6 ? $"h{text.HeadingLevel}" : "span";
        html.Append($"<{tag} style=\"");
        if (tag != "span") html.Append("margin: 0; display: inline; ");
        html.Append($"font-size: {Px(style.Size)}; ")
            .Append($"line-height: {Px(style.LineHeight)}; ")
            .Append($"font-weight: {(int)style.Weight}; ");
        if (style.Tracking != 0)
            html.Append($"letter-spacing: {Px(style.Tracking)}; ");
        if (style.Italic || text.Italic)
            html.Append("font-style: italic; ");
        if (text.Tabular)
            html.Append("font-variant-numeric: tabular-nums; ");
        html.Append($"font-family: {Family(text.Mono || style.Mono)}; ")
            .Append($"color: {ink}\">");

        if (text.Spans is { Count: > 0 } spans)
            foreach (var run in spans) WriteRun(run, theme, html);
        else
            html.Append(Text(text.PlainContent));

        html.Append($"</{tag}>");
        if (aligned) html.Append("</div>");
    }

    private static string CrossOf(CrossAlign cross) => cross switch
    {
        CrossAlign.Start => "top",
        CrossAlign.End => "bottom",
        // vertical-align cannot stretch; top is the least surprising reading of "fill the row".
        CrossAlign.Stretch => "top",
        _ => "middle",
    };

    /// <summary>One run: only what it OVERRIDES is declared, and a destination makes it an anchor.</summary>
    private static void WriteRun(TextRun run, IAppTheme theme, StringBuilder html)
    {
        var overrides = new StringBuilder();
        if (run.Color is { } color) overrides.Append($"color: {Literal(color, theme)}; ");
        if (run.Weight is { } weight) overrides.Append($"font-weight: {(int)weight}; ");
        if (run.Italic) overrides.Append("font-style: italic; ");
        if (run.Mono) overrides.Append($"font-family: {Family(mono: true)}; ");
        // The run-level escape hatch — inline code at 13.5 inside a 16 paragraph — carries its own
        // size and line, exactly as Text.StyleOverride does at paragraph level.
        if (run.StyleOverride is { } runStyle)
            overrides.Append($"font-size: {Px(runStyle.Size)}; line-height: {Px(runStyle.LineHeight)}; ");

        var tag = run.Destination is not null ? "a" : overrides.Length > 0 ? "span" : null;
        if (tag is null)
        {
            html.Append(Text(run.Content));
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
        html.Append('>').Append(Text(run.Content)).Append($"</{tag}>");
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
        // Per-corner, because CornerRadii is per-corner and collapsing to TopLeft changed shapes.
        if (box.Style.CornerRadius is { } radius && radius != CornerRadii.Zero)
            cell.Append("; border-radius: ").Append(Radius(radius));

        // The inside border, in the forms CSS can say inline. Start/End resolve as left/right, the
        // same LTR promise the padding makes.
        if (box.Style is { BorderWidth: > 0, BorderSides: not BorderSides.None } bordered)
        {
            var line = $"{Px(bordered.BorderWidth)} solid {Literal(bordered.BorderColor, context.Theme)}";
            if (bordered.BorderSides == BorderSides.All)
                cell.Append($"; border: {line}");
            else
            {
                if (bordered.BorderSides.HasFlag(BorderSides.Top)) cell.Append($"; border-top: {line}");
                if (bordered.BorderSides.HasFlag(BorderSides.End)) cell.Append($"; border-right: {line}");
                if (bordered.BorderSides.HasFlag(BorderSides.Bottom)) cell.Append($"; border-bottom: {line}");
                if (bordered.BorderSides.HasFlag(BorderSides.Start)) cell.Append($"; border-left: {line}");
            }
        }

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
        html.Append($"alt=\"{EscapeAttribute(image.Label)}\" ")
            .Append($"style=\"display: block; border: 0; width: {Px(image.Width)}; ")
            .Append(image.Fit == ImageFit.Contain ? "height: auto" : $"height: {Px(image.Height)}");
        if (image.CornerRadius != CornerRadii.Zero)
            html.Append("; border-radius: ").Append(Radius(image.CornerRadius));
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

        // Nested anchors are invalid HTML with an ambiguous destination — and both were said by the
        // AUTHOR, so neither can silently win.
        if (link.Child is Text { Spans: { Count: > 0 } } inner
            && inner.Spans.Any(run => run.Destination is not null))
            throw new NotSupportedException(
                "A Link around a Text that contains linked runs would nest anchors, which no HTML "
                + "allows. Link the runs, or the paragraph — one of them.");

        var textual = link.Child is Text;
        html.Append($"<a href=\"{EscapeAttribute(link.Destination)}\" ");
        if (!string.IsNullOrEmpty(link.Label))
            html.Append($"aria-label=\"{EscapeAttribute(link.Label)}\" ");
        html.Append("")
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
        {
            if (address.Length <= "mailto:".Length)
                throw new NotSupportedException($"{what} has a mailto: with nobody in it.");
            return;
        }
        // A real parse, not a prefix check: "https://" alone, or a host-less address, passed the
        // prefix and became a guaranteed-broken src/href — the exact thing this fence exists for.
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || uri.Host.Length == 0)
            throw new NotSupportedException(
                $"{what} needs an absolute http(s) address ('{address}' is not): the reader's "
                + "client opens the message with no origin to resolve it against.");
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
            (byte)MathF.Round(color.R * alpha + surface.R * (1 - alpha)),
            (byte)MathF.Round(color.G * alpha + surface.G * (1 - alpha)),
            (byte)MathF.Round(color.B * alpha + surface.B * (1 - alpha))));
    }

    internal static string Hex(Color color) => $"#{color.R:x2}{color.G:x2}{color.B:x2}";

    /// <summary>A CSS length, invariant and fraction-preserving: 16 → "16px", 13.5 → "13.5px".</summary>
    internal static string Px(float value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture) + "px";

    private static string Family(bool mono) => mono
        ? "ui-monospace, 'Cascadia Mono', 'Segoe UI Mono', Menlo, Consolas, monospace"
        : "-apple-system, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif";

    /// <summary>Four corners in CSS order, one value when uniform.</summary>
    private static string Radius(CornerRadii radius) =>
        radius.TopLeft == radius.TopRight && radius.TopRight == radius.BottomRight
            && radius.BottomRight == radius.BottomLeft
            ? Px(radius.TopLeft)
            : $"{Px(radius.TopLeft)} {Px(radius.TopRight)} {Px(radius.BottomRight)} {Px(radius.BottomLeft)}";

    internal static string Escape(string text) => text
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>
    /// Body text: escaped, with authored line breaks kept as <c>&lt;br&gt;</c> — HTML collapses a
    /// raw newline to a space, and <c>white-space</c> is exactly the kind of CSS Word ignores.
    /// </summary>
    internal static string Text(string content) => Escape(content).Replace("\n", "<br>");

    /// <summary>
    /// Attribute values additionally escape the QUOTE: an alt or URL containing one would end the
    /// attribute early and inject markup into the mail — and a plain query-string <c>&amp;</c>,
    /// unescaped, is malformed HTML some clients then re-write.
    /// </summary>
    internal static string EscapeAttribute(string value) => Escape(value).Replace("\"", "&quot;");
}
