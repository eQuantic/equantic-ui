namespace eQuantic.UI.Primitives;

/// <summary>
/// The style a <see cref="Text"/> is actually set in: the ROLE the theme cut, what the NODE said on
/// top of it, and the theme's code face when the result is monospaced.
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
/// time.
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
        var style = text.StyleOverride ?? theme.Type(text.Role);
        // The node ADDS to what the role said; it never takes away. `Text(…, mono: true)` on an
        // upright role is code inside prose, and an unset flag is "the role decides", not "no".
        if (text.Mono) style = style with { Mono = true };
        if (text.Italic) style = style with { Italic = true };
        return style.WithCodeFace(theme);
    }

    /// <summary>
    /// The theme's code face, when this style is monospaced and named no face of its own. A style
    /// that NAMES a family keeps it: the call site was specific, and specificity wins over a theme
    /// default the way it does everywhere else here.
    /// </summary>
    public static TypeStyle WithCodeFace(this TypeStyle style, IAppTheme theme) =>
        style.Mono && style.Family is null && theme.MonoFamily is { Length: > 0 } code
            ? style with { Family = code }
            : style;
}
