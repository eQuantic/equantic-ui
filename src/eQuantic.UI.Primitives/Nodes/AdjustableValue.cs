using System.Globalization;

namespace eQuantic.UI.Primitives;

/// <summary>
/// WHERE an <see cref="Adjustable"/>'s value sits, for the roles that have one — the trio ARIA
/// calls <c>aria-valuenow</c>, <c>aria-valuemin</c> and <c>aria-valuemax</c>, plus the words to say
/// it in.
///
/// <para>
/// ONE type rather than three properties on the node, because the three are only meaningful
/// together: <c>role="slider"</c> REQUIRES <c>aria-valuenow</c>, and a now without its bounds
/// announces a number against a range assistive tech has to assume (ARIA's own default is 0–100,
/// which is wrong for every slider in this design system). A node that could carry a value with no
/// range would let a control be half-announced, and the half that is missing is the half that makes
/// the number mean anything.
/// </para>
///
/// <para>
/// <see cref="Text"/> is the value SPOKEN when the number alone is not it — "R$ 400", "40%",
/// "Large". A reader that has it says it INSTEAD of the number, which is the whole point: 0.4 is
/// what the control holds and "40%" is what the user set.
/// </para>
/// </summary>
/// <param name="Now">The value the control currently holds.</param>
/// <param name="Min">The low end of the range it moves over.</param>
/// <param name="Max">The high end.</param>
public readonly record struct AdjustableValue(float Now, float Min, float Max)
{
    /// <summary>
    /// The value in words, when the number is not what a person would say — a currency, a unit, a
    /// named step. Null leaves the number to speak for itself, which is right for a bare ratio.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// What a reader announces: <see cref="Text"/> when the caller gave one, the number otherwise.
    /// INVARIANT, because this is the same string both realizers hand their platform — the web as
    /// an attribute and Photon as the semantic node's value — and a decimal comma on one target and
    /// a point on the other would be two announcements of one number.
    /// </summary>
    public string Spoken => Text is { Length: > 0 } text
        ? text
        : Now.ToString("0.####", CultureInfo.InvariantCulture);
}
