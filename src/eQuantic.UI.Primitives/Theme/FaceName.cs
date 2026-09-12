namespace eQuantic.UI.Primitives;

/// <summary>
/// What may be spelled as a font family, and what may not. One rule, asked before a family is
/// EMBEDDED anywhere, because a family is the only free-form text the style pipeline carries.
///
/// <para>
/// The alternative was escaping, and escaping is the wrong tool here. The family lands inside a
/// <c>&lt;style&gt;</c> element and inside a JSON payload in a <c>&lt;script&gt;</c> element, and
/// neither CSS strings nor JSON strings neutralise <c>&lt;/style&gt;</c> or <c>&lt;/script&gt;</c>
/// for the HTML parser on their own — the fix is a CSS hex escape and a <c><</c>, spelled
/// differently in each context. Two emitters in two languages reproducing two escape grammars
/// identically is a divergence waiting to happen, and this repo's own cross-pins exist because that
/// class of divergence is real. A PREDICATE is one rule, and both sides can hold it.
/// </para>
///
/// <para>
/// The rule is deliberately narrow, the way <see cref="Icon"/>'s size whitelist is: a real family
/// name is letters, digits, spaces and a little punctuation ("IBM Plex Sans", "JetBrainsMono Nerd
/// Font", ".AppleSystemUIFont", "Noto Sans CJK JP"). Anything else is not a family this can spell,
/// and a family it cannot spell is reported rather than embedded — the same outcome CoreText
/// reaches by substituting, recorded in the same place.
/// </para>
/// </summary>
public static class FaceName
{
    /// <summary>The longest family anyone ships, with room: past this it is not a name.</summary>
    private const int MaxLength = 128;

    /// <summary>
    /// Whether <paramref name="family"/> can be embedded as written. Unicode LETTERS and digits are
    /// allowed, so a CJK or Cyrillic family passes; the punctuation is the set real families use
    /// and nothing that means something to CSS, JSON or HTML.
    /// </summary>
    public static bool IsWellFormed(string? family)
    {
        if (family is not { Length: > 0 and <= MaxLength }) return false;
        if (char.IsWhiteSpace(family[0]) || char.IsWhiteSpace(family[^1])) return false;

        foreach (var c in family)
        {
            // Space only — never a tab or a newline, which a CSS string cannot carry raw anyway.
            var allowed = c == ' ' || c is '-' or '_' or '.' or '+'
                || char.IsLetterOrDigit(c);
            if (!allowed) return false;
        }

        return true;
    }

    /// <summary>
    /// The family when it can be embedded, <c>null</c> when it cannot — and a rejection is RECORDED
    /// as unresolved on its way out. A name no emitter will accept is a face this run asked for and
    /// did not get, which is the fact <see cref="FaceResolution"/> already exists to carry: it
    /// reaches the run line beside the families the machine simply lacks, rather than vanishing.
    /// </summary>
    public static string? Usable(string? family)
    {
        if (IsWellFormed(family)) return family;
        if (family is { Length: > 0 }) FaceResolution.Missing(family);
        return null;
    }
}
