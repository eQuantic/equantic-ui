using System.Text;

namespace eQuantic.UI.Primitives;

/// <summary>
/// What may be spelled as a font family, and what may not. One rule, asked before a family is
/// EMBEDDED anywhere, because a family is the only free-form text the style pipeline carries.
///
/// <para>
/// The alternative was escaping, and escaping is the wrong tool here. The family lands inside a
/// <c>&lt;style&gt;</c> element and inside a JSON payload in a <c>&lt;script&gt;</c> element, and
/// neither CSS strings nor JSON strings neutralise <c>&lt;/style&gt;</c> or <c>&lt;/script&gt;</c>
/// for the HTML parser on their own — the fix is a CSS hex escape and a <c>\u003c</c>, spelled
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
        // Space exactly, not `char.IsWhiteSpace`: the twin tests `=== ' '`, and the set below is
        // the only other place whitespace could enter.
        if (family[0] == ' ' || family[^1] == ' ') return false;

        // RUNES, not chars. `char.IsLetterOrDigit` reads one UTF-16 code unit, so each half of a
        // surrogate pair answers false and a supplementary-plane letter (U+10400, and every CJK
        // extension past the BMP) is rejected here while the twin's `\p{L}` under /u accepts it.
        // SSR would omit the face and hydration would embed it — the divergence this rule exists to
        // prevent, introduced by the rule itself.
        foreach (var rune in family.EnumerateRunes())
        {
            var allowed = rune.Value is ' ' or '-' or '_' or '.' or '+'
                || Rune.IsLetterOrDigit(rune);
            if (!allowed) return false;
        }

        return true;
    }

    /// <summary>
    /// The family when it can be embedded, <c>null</c> when it cannot — and a rejection is RECORDED
    /// as unresolved on its way out. A name no emitter will accept is a face this run asked for and
    /// did not get, which is the fact <see cref="FaceResolution"/> already exists to carry: it
    /// reaches the run line beside the families the machine simply lacks, rather than vanishing.
    /// <para>
    /// HOST ONLY, and that is why: it writes <see cref="FaceResolution"/>, which owes no browser
    /// twin — the report exists because CoreText, DirectWrite and Android substitute in silence,
    /// and a browser names its own fallback in the declaration instead. A transpiled component
    /// calling this would resolve to nothing at runtime, so it says so here rather than there.
    /// <see cref="IsWellFormed"/> is the half that crosses, and it crosses.
    /// </para>
    /// </summary>
    [ServerOnly]
    public static string? Usable(string? family)
    {
        if (IsWellFormed(family)) return family;
        if (family is { Length: > 0 }) FaceResolution.Missing(family);
        return null;
    }
}
