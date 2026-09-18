using System.Text;
using eQuantic.UI.Primitives;
using static eQuantic.UI.Email.EmailRealizer;

namespace eQuantic.UI.Email;

/// <summary>
/// The HTML alternative: the seven words this medium carries, written as the nested tables and
/// inline styles an email client will actually render. What it does NOT carry is
/// <see cref="EmailWalk"/>'s to refuse, shared with the plain-text alternative so the two parts of
/// one message cannot disagree about the tree.
/// <para>
/// One instance per message — the context and the builder are fixed for the pass, so they live on
/// the visitor and the walk carries <see cref="Nothing"/> down.
/// </para>
/// </summary>
internal sealed class EmailVisitor(ComponentContext context, StringBuilder html) : EmailWalk
{
    /// <inheritdoc/>
    public override Nothing Visit(UiComponent node, Nothing state)
    {
        using var _ = ComponentBoundary.Enter(node);
        return node.Build(context).Accept(this, state);
    }

    /// <summary>
    /// A Column is a table with one row per child. Its gap — which no email engine implements as a
    /// property — is a SPACER ROW with an explicit height, between children and never after the
    /// last, which is exactly what <c>gap</c> means.
    /// <para>
    /// MainAlign is VACUOUS here, on both axes, and that is documented rather than fenced: an email
    /// table sizes to its content, so there is no free space for Start/Center/End to distribute and
    /// no gap for SpaceBetween to widen — every value renders identically, which is faithful, not
    /// divergent. Cross is real (a narrower child sits somewhere in a full-width column) and maps
    /// to the cell's align; padding wraps the table in the one-cell shell email understands.
    /// </para>
    /// </summary>
    public override Nothing Visit(Column node, Nothing state)
    {
        OpenPadding(node.Padding);
        html.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"border-collapse: collapse\">");
        var first = true;
        foreach (var child in node.Children)
        {
            if (!first && node.Gap > 0)
                html.Append($"<tr><td style=\"height: {Px(node.Gap)}; line-height: {Px(node.Gap)}; font-size: 0\">&nbsp;</td></tr>");
            first = false;

            // Per CHILD, because AlignSelf overrides the column's cross for that child alone —
            // each child owns a cell here, so the medium expresses it for free.
            var align = (child.AlignSelf ?? node.Cross) switch
            {
                CrossAlign.Center => " align=\"center\"",
                CrossAlign.End => " align=\"right\"",
                _ => "",
            };
            html.Append($"<tr><td{align} style=\"vertical-align: top\">");
            child.Accept(this, state);
            html.Append("</td></tr>");
        }
        html.Append("</table>");
        ClosePadding(node.Padding);
        return state;
    }

    /// <summary>A Row is exactly one table row; its gap is a spacer CELL with an explicit width.</summary>
    public override Nothing Visit(Row node, Nothing state)
    {
        OpenPadding(node.Padding);
        html.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"border-collapse: collapse\"><tr>");
        var first = true;
        foreach (var child in node.Children)
        {
            if (!first && node.Gap > 0)
                html.Append($"<td style=\"width: {Px(node.Gap)}; font-size: 0\">&nbsp;</td>");
            first = false;

            html.Append($"<td style=\"vertical-align: {CrossOf(child.AlignSelf ?? node.Cross)}\">");
            child.Accept(this, state);
            html.Append("</td>");
        }
        html.Append("</tr></table>");
        ClosePadding(node.Padding);
        return state;
    }

    /// <summary>
    /// A Text carries the theme's ramp INLINE — size, line-height, weight, tracking, slant and
    /// face — and its ink as one literal color. Runs render as nested inline elements so a bold
    /// word, a colored fragment or a LINKED run survives into the mail; flattening them was a
    /// silent loss, and a linked run in particular became text the reader could not act on.
    /// </summary>
    public override Nothing Visit(Text node, Nothing state)
    {
        // The two Text options this medium cannot express, fenced rather than approximated:
        // gradient ink (background-clip: text is not email CSS — a solid stand-in would be a
        // different ink nobody chose) and MaxLines (no client clamps lines, and showing MORE text
        // than the author bounded is a content divergence, not a style one).
        if (node.Gradient is not null)
            throw new NotSupportedException(
                "Gradient text cannot be realized in an email — no client paints it. Give the "
                + "email's text a solid color.");
        if (node.MaxLines > 0)
            throw new NotSupportedException(
                "MaxLines cannot be realized in an email — no client clamps lines, so the whole "
                + "text would show. Shorten the content for this medium instead.");

        var theme = context.Theme;
        var style = node.Resolve(theme);
        var ink = Literal(node.Color ?? theme.TextPrimary, theme);

        // text-align applies to blocks, so an aligned paragraph gets a block wrapper; a start-
        // aligned one (the default) stays inline and composes inside Rows and Links unchanged.
        var aligned = node.Align != TextAlignment.Start;
        if (aligned)
            html.Append($"<div style=\"text-align: {(node.Align == TextAlignment.Center ? "center" : "right")}\">");

        // A heading level keeps its ELEMENT — outline navigation in the clients and screen readers
        // that offer it — with margin zeroed so the tag is semantics, not layout.
        var tag = node.HeadingLevel is >= 1 and <= 6 ? $"h{node.HeadingLevel}" : "span";
        html.Append($"<{tag} style=\"");
        if (tag != "span") html.Append("margin: 0; display: inline; ");
        html.Append($"font-size: {Px(style.Size)}; ")
            .Append($"line-height: {Px(style.LineHeight)}; ")
            .Append($"font-weight: {(int)style.Weight}; ");
        if (style.Tracking != 0)
            html.Append($"letter-spacing: {Px(style.Tracking)}; ");
        if (style.Italic)
            html.Append("font-style: italic; ");
        if (node.Tabular)
            html.Append("font-variant-numeric: tabular-nums; ");
        html.Append($"font-family: {Family(style.Family, style.Mono)}; ")
            .Append($"color: {ink}\">");

        if (node.Spans is { Count: > 0 } spans)
            foreach (var run in spans) WriteRun(run, theme);
        else
            html.Append(EmailRealizer.Text(node.PlainContent));

        html.Append($"</{tag}>");
        if (aligned) html.Append("</div>");
        return state;
    }

    /// <summary>A Box paints a background and padding on its cell — the two things a Box means here.</summary>
    public override Nothing Visit(Box node, Nothing state)
    {
        var cell = new StringBuilder("vertical-align: top");

        if (node.Style.Background is { } background)
            cell.Append($"; background-color: {Literal(background, context.Theme)}");
        // Start/End resolve as left/right: an email body is LTR unless the document says otherwise,
        // and direction-aware layout is beyond what this medium can promise.
        if (node.Style.Padding is { } padding)
            cell.Append("; padding: ").Append(
                padding.Top == padding.End && padding.End == padding.Bottom && padding.Bottom == padding.Start
                    ? Px(padding.Top)
                    : $"{Px(padding.Top)} {Px(padding.End)} {Px(padding.Bottom)} {Px(padding.Start)}");
        // A radius is honoured by the clients that can (Apple Mail, Gmail) and squarely ignored by
        // Word's engine — a degradation, not a divergence: the box is the same box with corners.
        // Per-corner, because CornerRadii is per-corner and collapsing to TopLeft changed shapes.
        if (node.Style.CornerRadius is { } radius && radius != CornerRadii.Zero)
            cell.Append("; border-radius: ").Append(Radius(radius));

        // The inside border, in the forms CSS can say inline. Start/End resolve as left/right, the
        // same LTR promise the padding makes.
        if (node.Style is { BorderWidth: > 0, BorderSides: not BorderSides.None } bordered)
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
        if (node.Child is { } child) child.Accept(this, state);
        html.Append("</td></tr></table>");
        return state;
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
    public override Nothing Visit(Image node, Nothing state)
    {
        RequireAbsolute(node.Source, "An image in an email", allowMailto: false);

        // One mode, decided once: colors take the theme's light leg, artwork takes the light source.
        html.Append($"<img src=\"{EscapeAttribute(node.Source)}\" width=\"{(int)node.Width}\" ");
        if (node.Fit != ImageFit.Contain)
            html.Append($"height=\"{(int)node.Height}\" ");
        html.Append($"alt=\"{EscapeAttribute(node.Label)}\" ")
            .Append($"style=\"display: block; border: 0; width: {Px(node.Width)}; ")
            .Append(node.Fit == ImageFit.Contain ? "height: auto" : $"height: {Px(node.Height)}");
        if (node.CornerRadius != CornerRadii.Zero)
            html.Append("; border-radius: ").Append(Radius(node.CornerRadius));
        html.Append("\">");
        return state;
    }

    /// <summary>
    /// A Link is a destination and a child; the child says what it looks like. A TEXT child is
    /// underlined — there is no hover in this medium to reveal a link, so it must look like one. A
    /// painted child (the bulletproof-button pattern: a Box around a label) is not — the box is the
    /// affordance — and the anchor is inline-block so it takes the box's size.
    /// </summary>
    public override Nothing Visit(Link node, Nothing state)
    {
        RequireAbsolute(node.Destination, "A link in an email", allowMailto: true);

        // Nested anchors are invalid HTML with an ambiguous destination — and both were said by the
        // AUTHOR, so neither can silently win.
        if (node.Child is Text { Spans: { Count: > 0 } } inner
            && inner.Spans.Any(run => run.Destination is not null))
            throw new NotSupportedException(
                "A Link around a Text that contains linked runs would nest anchors, which no HTML "
                + "allows. Link the runs, or the paragraph — one of them.");

        var textual = node.Child is Text;
        html.Append($"<a href=\"{EscapeAttribute(node.Destination)}\" ");
        if (!string.IsNullOrEmpty(node.Label))
            html.Append($"aria-label=\"{EscapeAttribute(node.Label)}\" ");
        html.Append("")
            .Append(textual
                ? "style=\"text-decoration: underline; color: inherit\">"
                : "style=\"text-decoration: none; color: inherit; display: inline-block\">");
        node.Child.Accept(this, state);
        html.Append("</a>");
        return state;
    }

    // ---- what the arms are written with -------------------------------------------------------

    /// <summary>Container padding, in the one form every client honours: a one-cell wrapper table.</summary>
    private void OpenPadding(EdgeInsets padding)
    {
        if (padding == EdgeInsets.Zero) return;
        var value = padding.Top == padding.End && padding.End == padding.Bottom && padding.Bottom == padding.Start
            ? Px(padding.Top)
            : $"{Px(padding.Top)} {Px(padding.End)} {Px(padding.Bottom)} {Px(padding.Start)}";
        html.Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"border-collapse: collapse\"><tr><td style=\"padding: {value}\">");
    }

    private void ClosePadding(EdgeInsets padding)
    {
        if (padding != EdgeInsets.Zero) html.Append("</td></tr></table>");
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
    private void WriteRun(TextRun run, IAppTheme theme)
    {
        var overrides = new StringBuilder();
        if (run.Color is { } color) overrides.Append($"color: {Literal(color, theme)}; ");
        if (run.Weight is { } weight) overrides.Append($"font-weight: {(int)weight}; ");
        if (run.Italic) overrides.Append("font-style: italic; ");
        // A mono RUN takes the THEME's code face, not the paragraph's. Those are two different
        // things and the first version conflated them: not inheriting the paragraph's proportional
        // family is right, and it does not follow that the run gets no family at all. The web picks
        // this up for free because its mono stack is `var(--eq-font-mono, …)` and the sheet declares
        // that from the theme; email has no variable to read, so it asks.
        if (run.Mono) overrides.Append($"font-family: {Family(theme.MonoFamily, mono: true)}; ");
        // The run-level escape hatch — inline code at 13.5 inside a 16 paragraph — carries its own
        // size and line, exactly as Text.StyleOverride does at paragraph level.
        if (run.StyleOverride is { } runStyle)
            overrides.Append($"font-size: {Px(runStyle.Size)}; line-height: {Px(runStyle.LineHeight)}; ");

        var tag = run.Destination is not null ? "a" : overrides.Length > 0 ? "span" : null;
        if (tag is null)
        {
            html.Append(EmailRealizer.Text(run.Content));
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
        html.Append('>').Append(EmailRealizer.Text(run.Content)).Append($"</{tag}>");
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
    /// The face a theme named, quoted in front of the stack this target falls back to.
    ///
    /// <para>
    /// Email has stacks of ITS OWN rather than the web's, and deliberately: there is no
    /// <c>var()</c> to read here — custom properties are unsupported across most clients — so the
    /// fallbacks are spelled out and chosen for what mail clients actually have. The NAMED face
    /// goes in front for the same reason it does on the web: a recipient whose machine has the
    /// brand renders the design, and everyone else lands on a stack that was chosen rather than
    /// whatever the client felt like. No registration is possible here and none is implied.
    /// </para>
    ///
    /// <para>
    /// The effective style is already merged upstream (<c>text.StyleOverride ?? theme.Type(role)</c>),
    /// so unlike the web there is no role-versus-node split to keep: email renders once and is never
    /// hydrated, which is exactly why the role's face can be written inline here and must not be
    /// there.
    /// </para>
    /// </summary>
    private static string Family(string? family, bool mono)
    {
        var stack = mono
            ? "ui-monospace, 'Cascadia Mono', 'Segoe UI Mono', Menlo, Consolas, monospace"
            : "-apple-system, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif";
        return FaceName.Usable(family) is { } face ? $"\"{face}\", {stack}" : stack;
    }

    /// <summary>Four corners in CSS order, one value when uniform.</summary>
    private static string Radius(CornerRadii radius) =>
        radius.TopLeft == radius.TopRight && radius.TopRight == radius.BottomRight
            && radius.BottomRight == radius.BottomLeft
            ? Px(radius.TopLeft)
            : $"{Px(radius.TopLeft)} {Px(radius.TopRight)} {Px(radius.BottomRight)} {Px(radius.BottomLeft)}";
}
