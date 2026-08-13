namespace eQuantic.UI.Compiler.Services;

/// <summary>One placeholder read out of a composite format template.</summary>
public readonly record struct TemplateHole(int Index, string Spec);

/// <summary>
/// The D7 formatting subset, as the BUILD knows it (docs/I18N-PLAN.md §3).
/// <para>
/// The runtime's formatter and this list are two halves of one promise: a specifier is in the
/// subset when the browser can be made to print exactly what .NET prints, and the cross-pinned
/// fixture (FormatSubsetTests ↔ format-subset.spec.ts) is the proof for every one of them. What
/// cannot be reproduced is refused HERE, where the developer is still holding the keyboard, rather
/// than diverging silently in front of a reader.
/// </para>
/// <para>
/// Out, and each for a reason worth stating: alignment (<c>{0,10}</c>) pads to a width the
/// browser measures differently for the same string; <c>X</c> prints a negative number as two's
/// complement at the C# TYPE's width, and JavaScript hands the runtime one untyped number; <c>R</c>
/// and <c>G17</c> are round-trip forms whose digit choice is the CLR's, not ICU's.
/// </para>
/// </summary>
public static class FormatSubset
{
    /// <summary>Standard NUMERIC specifiers, without their optional precision digits.</summary>
    private static readonly char[] NumericStandard = ['N', 'F', 'P', 'C', 'D', 'E'];

    /// <summary>Standard DATE/TIME specifiers — the culture patterns the catalog carries, plus the
    /// invariant round-trip forms.</summary>
    private static readonly char[] DateStandard = ['d', 'D', 't', 'T', 'g', 'G', 'f', 'F', 'M', 'm', 'Y', 'y', 'O', 'o', 's'];

    /// <summary>The tokens a CUSTOM picture may use — digit places for numbers, the date/time parts
    /// the runtime's pattern renderer implements, and the punctuation that separates them.</summary>
    private static bool IsCustomPictureChar(char c) =>
        c is '0' or '#' or ',' or '.' or '%' or '\''
            or 'y' or 'M' or 'd' or 'H' or 'h' or 'm' or 's' or 't' or 'f' or 'F' or 'K' or 'z'
            or ' ' or '-' or '/' or ':' or '\\';

    /// <summary>
    /// Whether a specifier survives the trip. Empty is fine — it is the plain <c>{0}</c>, which the
    /// runtime formats through the culture exactly as .NET does.
    /// </summary>
    public static bool Allows(string spec)
    {
        if (spec.Length == 0) return true;

        // A STANDARD specifier is one letter, optionally followed by a precision.
        if (spec.Length <= 3)
        {
            var head = spec[0];
            var digits = spec[1..];
            var precisionOk = digits.Length == 0 || int.TryParse(digits, out _);
            if (precisionOk && Array.IndexOf(DateStandard, head) >= 0 && digits.Length == 0) return true;
            if (precisionOk && Array.IndexOf(NumericStandard, head) >= 0) return true;
            // Lowercase numeric standards are the same specifiers ('n2' == 'N2').
            if (precisionOk && Array.IndexOf(NumericStandard, char.ToUpperInvariant(head)) >= 0
                && !char.IsUpper(head) && Array.IndexOf(DateStandard, head) < 0)
                return true;
        }

        // Otherwise it must be a custom picture made only of tokens the runtime renders.
        foreach (var c in spec)
            if (!IsCustomPictureChar(c))
                return false;
        return true;
    }

    /// <summary>
    /// Reads a composite template's placeholders. Returns null with a <paramref name="error"/> when
    /// the template is not a valid composite format at all, or uses something outside the subset.
    /// </summary>
    public static IReadOnlyList<TemplateHole>? Read(string template, out string? error)
    {
        error = null;
        var holes = new List<TemplateHole>();
        for (var i = 0; i < template.Length; i++)
        {
            var c = template[i];
            if (c == '{' && i + 1 < template.Length && template[i + 1] == '{') { i++; continue; }
            if (c == '}' && i + 1 < template.Length && template[i + 1] == '}') { i++; continue; }
            if (c == '}')
            {
                error = "a lone '}' is not a valid composite format";
                return null;
            }
            if (c != '{') continue;

            var close = template.IndexOf('}', i + 1);
            if (close < 0)
            {
                error = "unterminated placeholder";
                return null;
            }

            var token = template[(i + 1)..close];
            if (token.Contains(','))
            {
                error = $"'{{{token}}}' uses alignment, which pads to a width the browser measures "
                    + "differently — format the value into the string instead";
                return null;
            }

            var colon = token.IndexOf(':');
            var indexText = colon < 0 ? token : token[..colon];
            var spec = colon < 0 ? "" : token[(colon + 1)..];

            if (!int.TryParse(indexText, out var index))
            {
                error = $"'{{{token}}}' is not a positional placeholder";
                return null;
            }
            if (!Allows(spec))
            {
                error = $"'{{{token}}}' uses the format specifier '{spec}', which is outside the "
                    + "subset the browser can reproduce exactly (docs/I18N-PLAN.md D7)";
                return null;
            }

            holes.Add(new TemplateHole(index, spec));
            i = close;
        }
        return holes;
    }
}
