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
/// <para>
/// THIS CLASS IS THE ENTRY POINT AND THE MEDIUM'S FORMATTING — the literal color, the CSS length,
/// the escaping every part of a message shares. WHICH nodes cross and what each becomes belongs to
/// <see cref="EmailWalk"/> and <see cref="EmailVisitor"/>, where the compiler can ask the question
/// of every word in the vocabulary instead of a switch falling through.
/// </para>
/// </summary>
public static class EmailRealizer
{
    /// <summary>The lowered tree as an HTML fragment — tables, inline styles, literal colors.</summary>
    public static string Lower(VisualNode node, IAppTheme theme)
    {
        var html = new StringBuilder();
        node.Accept(new EmailVisitor(new ComponentContext(theme), html), default);
        return html.ToString();
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
