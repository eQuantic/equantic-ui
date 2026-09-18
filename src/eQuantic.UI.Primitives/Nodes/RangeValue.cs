using System.Globalization;

namespace eQuantic.UI.Primitives;

/// <summary>
/// WHERE a value sits between two ends, and the words to say it in — the quartet ARIA gives every
/// RANGE node: <c>aria-valuenow</c>, <c>aria-valuemin</c>, <c>aria-valuemax</c> and
/// <c>aria-valuetext</c>.
///
/// <para>
/// It is named for what it IS rather than for the first node that wanted it. This arrived as
/// <c>AdjustableValue</c> with <see cref="Adjustable"/>, and the second node to need it —
/// <see cref="Progress"/> — is not adjustable at all: a progress bar reports, it does not move.
/// The quartet is the same one either way, so one type carries it and neither node's name is
/// stamped on the other's.
/// </para>
///
/// <para>
/// ONE type rather than three properties on a node, because the three are only meaningful together:
/// a now without its bounds is announced against a range assistive tech has to assume (ARIA's own
/// default is 0–100, which is wrong for every control in this design system). A node that could
/// carry a value with no range would let a control be half-announced, and the half that is missing
/// is the half that makes the number mean anything.
/// </para>
///
/// <para>
/// <see cref="Text"/> is the value SPOKEN when the number alone is not it — "R$ 400", "40%",
/// "Large", "3 of 7". A reader that has it says it INSTEAD of the number, which is the whole point:
/// 0.4 is what the control holds and "40%" is what a person would say.
/// </para>
/// </summary>
/// <param name="Now">The value the node currently reports.</param>
/// <param name="Min">The low end of the range it covers.</param>
/// <param name="Max">The high end.</param>
public readonly record struct RangeValue(float Now, float Min, float Max)
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
