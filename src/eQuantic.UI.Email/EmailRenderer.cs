using System.Text;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Email;

/// <summary>Both parts of a multipart message, generated from ONE tree so they cannot drift.</summary>
public sealed record EmailMessage(string Html, string PlainText);

/// <summary>
/// Renders a component as a complete email: the document shell every client tolerates around the
/// lowered tree, and the <c>text/plain</c> alternative walked from the same tree.
/// <para>
/// The shell is deliberately minimal — doctype, one centered 600px table, a solid page background,
/// and an invisible preheader when given. No <c>&lt;style&gt;</c> block, ever: one rule beats a
/// matrix of client exceptions. Sending is the app's job (MailKit, SES, whatever it already uses);
/// what leaves here is content.
/// </para>
/// </summary>
public static class EmailRenderer
{
    /// <summary>The conventional width every client tolerates.</summary>
    private const int BodyWidth = 600;

    /// <summary>
    /// The ROOT expansion, and it enters the boundary like every other one — held open across the
    /// realization below it, so the depth a nested component sees counts this one too.
    /// <para>
    /// It was left outside at first, on the reasoning that the visitors bound everything after it so
    /// a cycle could not escape. True, and not the point: a <c>Build</c> outside the seam is a
    /// second place the rule lives, and it showed up as a test that could not tell whether the
    /// counter had been restored — the component it rendered afterwards was expanded here, without
    /// ever asking.
    /// </para>
    /// </summary>
    public static EmailMessage Render(UiComponent component, IAppTheme theme, string? preheader = null)
    {
        using var _ = ComponentBoundary.Enter(component);
        return Render(component.Build(new ComponentContext(theme)), theme, preheader);
    }

    public static EmailMessage Render(VisualNode tree, IAppTheme theme, string? preheader = null)
    {
        var body = EmailRealizer.Lower(tree, theme);
        return new EmailMessage(Shell(body, theme, preheader), PlainText(tree, theme));
    }

    private static string Shell(string body, IAppTheme theme, string? preheader)
    {
        var page = EmailRealizer.Hex(theme.Background.Resolve(ThemeMode.Light));
        var surface = EmailRealizer.Hex(theme.Surface.Resolve(ThemeMode.Light));

        var html = new StringBuilder();
        html.Append("<!DOCTYPE html>");
        html.Append("<html lang=\"en\" xmlns=\"http://www.w3.org/1999/xhtml\">");
        html.Append("<head>");
        html.Append("<meta charset=\"utf-8\">");
        html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        // Tell the clients that honour it that we render ONE scheme, so they do not invert the
        // literal colors we just committed to.
        html.Append("<meta name=\"color-scheme\" content=\"light\">");
        html.Append("<title></title>");
        html.Append("</head>");
        html.Append($"<body style=\"margin: 0; padding: 0; background-color: {page}\">");

        if (!string.IsNullOrEmpty(preheader))
        {
            // The inbox preview line: read by the list view, never by the eye. Hidden every way the
            // clients respect, because any one of them alone leaks on some client.
            html.Append("<div style=\"display: none; max-height: 0; overflow: hidden; mso-hide: all\">")
                .Append(EmailRealizer.Escape(preheader))
                .Append("</div>");
        }

        html.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"border-collapse: collapse\">");
        html.Append("<tr><td align=\"center\" style=\"padding: 24px 12px\">");
        html.Append($"<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"{BodyWidth}\" style=\"border-collapse: collapse; width: {BodyWidth}px; max-width: 100%; background-color: {surface}\">");
        html.Append("<tr><td style=\"padding: 24px\">");
        html.Append(body);
        html.Append("</td></tr></table>");
        html.Append("</td></tr></table>");
        html.Append("</body></html>");
        return html.ToString();
    }

    /// <summary>
    /// The text alternative, from the SAME tree: one line per text, a blank line where a Column
    /// gap separated sections. Writing it by hand is how the two parts drift; walking the tree is
    /// how they cannot.
    /// </summary>
    private static string PlainText(VisualNode node, IAppTheme theme)
    {
        var text = new StringBuilder();
        node.Accept(new EmailTextVisitor(theme, text), default);
        return text.ToString().Trim();
    }
}
