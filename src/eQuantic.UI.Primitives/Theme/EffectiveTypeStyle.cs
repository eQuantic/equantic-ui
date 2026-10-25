namespace eQuantic.UI.Primitives;

/// <summary>
/// The style a <see cref="Text"/> is actually set in: the ROLE the theme cut, what the NODE said on
/// top of it, and the theme's face when neither named one: its code face for monospaced text, its
/// text face for the rest.
///
/// <para>
/// One function because there were three, one per realizer, each merging the same three facts its
/// own way — Photon at <c>PhotonRealizer</c>, email at <c>EmailRealizer</c>, and the web spreading
/// the same merge across separate <c>mono</c>, <c>italic</c> and <c>face</c> locals. Two pieces
/// answering one question is how one of them gets forgotten, and this slice has already paid for
/// that lesson twice: a <c>withSize</c> that dropped the family, a measurer whose sans half was
/// missing for as long as its mono half existed.
/// </para>
///
/// <para>
/// The CODE FACE is the part that could not live anywhere else. <see cref="TypeStyle.Family"/> is
/// themeable per role and <see cref="TypeStyle.Mono"/> is a per-node flag, so a theme had no way to
/// name the face its monospaced text should use — the role never tells it which text wanted the
/// other one. Resolved HERE, where the role, the node and the theme are all in hand for the only
/// time. The TEXT FACE resolves beside it, for the same reason and by the same rule.
/// </para>
/// <para>
/// The web takes neither from here: its document's face is inherited, and its role classes name a
/// face only where the role has one, so a text the theme's face reaches is written with no family
/// of its own.
/// </para>
/// </summary>
[ServerOnly]
public static class EffectiveTypeStyle
{
    /// <summary>
    /// The role's style with the node's overrides applied. <paramref name="theme"/> supplies both
    /// the role and the code face.
    /// </summary>
    public static TypeStyle Resolve(this Text text, IAppTheme theme)
    {
        // WHERE the style came from, kept before it is flattened. `WithThemeFace` steps aside for a
        // style that NAMES a family, on the rule that the call site was specific — and after this
        // line nothing can tell a family the CALL SITE chose from one the ROLE supplied as a
        // default. Reported by the eQuantic Code IDE, whose theme is
        // `Base.Type(role) with { Family = brandSans }`: every role named a face, so `MonoFamily`
        // was inert for exactly the themes that brand their type, which is backwards. Naming a face
        // per role is the normal way to use `Type`.
        var chosen = text.StyleOverride is not null;
        var style = text.StyleOverride ?? theme.Type(text.Role);
        // Whether the role itself is CODE. A theme that sets `Mono` and a `Family` on the same role
        // chose that pairing deliberately and is more specific than the theme-wide code face, so it
        // is left alone — this is not "mono means drop the family".
        var roleIsCode = style.Mono;

        // The node ADDS to what the role said; it never takes away. `Text(…, mono: true)` on an
        // upright role is code inside prose, and an unset flag is "the role decides", not "no".
        if (text.Mono) style = style with { Mono = true };
        if (text.Italic) style = style with { Italic = true };

        // A PROPORTIONAL role face has no business surviving a node that asked for code. The three
        // conditions are the whole rule: the family came from the role rather than the call site,
        // the role was not code itself, and the node is what made this monospaced.
        if (!chosen && !roleIsCode && text.Mono) style = style with { Family = null };

        return style.WithThemeFace(theme);
    }

    /// <summary>
    /// The style a <see cref="TextEntry"/> is set in: its role, and the theme's face when the role
    /// named none. The field's twin of <see cref="Resolve(Text, IAppTheme)"/>: four places measured,
    /// drew and hit-tested a field's text from the role alone, so a theme's text face would have
    /// reached every label and missed the field a person types into.
    /// </summary>
    public static TypeStyle Resolve(this TextEntry entry, IAppTheme theme) =>
        theme.Type(entry.Role).WithThemeFace(theme);

    /// <summary>
    /// The style a RUN inside a paragraph is set in: the paragraph's, plus what the run said, plus
    /// the theme's faces. The run-level twin of <see cref="Resolve(Text, IAppTheme)"/>, and here
    /// for the reason that function exists at all — this merge was written out by hand in the layout
    /// engine and got the family wrong, which is what a second voice does.
    ///
    /// <para>
    /// The one asymmetry with the paragraph: a MONO run drops the inherited family. A run cannot
    /// NAME a face — <see cref="TextRun.StyleOverride"/> carries a size and nothing else — so a
    /// family arriving here came from the ROLE and was never chosen for this span, while a family on
    /// a paragraph was chosen and is kept. It matters because every shell resolves a face as
    /// <c>family ?? the OS fixed-pitch face</c>: an inherited proportional family wins over the mono
    /// flag, so a theme that gave its body role a face had inline code measured and drawn in that
    /// face on Photon while the web and email both set it monospaced.
    /// </para>
    /// </summary>
    public static TypeStyle Resolve(this TextRun run, TypeStyle paragraph, IAppTheme theme)
    {
        var style = paragraph;
        // Only the SIZE travels from a run's override — the run keeps the paragraph's line box,
        // which is what makes it a run rather than a line of its own.
        if (run.StyleOverride is { } over) style = style.WithSize(over.Size);
        if (run.Mono) style = style with { Mono = true, Family = null };
        if (run.Italic) style = style with { Italic = true };
        if (run.Weight is { } weight) style = style with { Weight = weight };
        return style.WithThemeFace(theme);
    }

    /// <summary>
    /// The theme's face, for a style that named none of its own: <see cref="IAppTheme.MonoFamily"/>
    /// when it is monospaced, <see cref="IAppTheme.TextFamily"/> when it is not. A style that NAMES a
    /// family keeps it: the call site or the role was specific, and specificity wins over a theme
    /// default the way it does everywhere else here.
    /// <para>
    /// A paragraph takes the text face here, and a monospaced run inside it drops the inherited
    /// family before it comes back (see <see cref="Resolve(TextRun, TypeStyle, IAppTheme)"/>), so the
    /// run is set in the code face and the words around it in the text face.
    /// </para>
    /// </summary>
    public static TypeStyle WithThemeFace(this TypeStyle style, IAppTheme theme) =>
        style.Family is null && (style.Mono ? theme.MonoFamily : theme.TextFamily) is { Name.Length: > 0 } face
            ? style with { Family = face }
            : style;
}
