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
/// THE WORDS ARE NOT HERE, and that is #243. They used to be (<c>Text</c>, and a <c>Spoken</c> that
/// preferred it), which tied them to a number that a <see cref="Progress"/> may not have: an
/// INDETERMINATE bar carries no <c>RangeValue</c> at all, so "Estimating time remaining" went with
/// the number that was never there — on both realizers, since both read the text from inside the
/// value. The words now live on the NODE (<c>Progress.ValueText</c>, <c>Adjustable.ValueText</c>),
/// which is the one place a reader looks whether or not there is a number to replace.
/// </para>
/// </summary>
/// <param name="Now">The value the node currently reports.</param>
/// <param name="Min">The low end of the range it covers.</param>
/// <param name="Max">The high end.</param>
public readonly record struct RangeValue(float Now, float Min, float Max)
{
    /// <summary>
    /// <see cref="Now"/> as a reader would hear it when no words replace it. INVARIANT, because
    /// this is the same string both realizers hand their platform — the web as an attribute and
    /// Photon as the semantic node's value — and a decimal comma on one target and a point on the
    /// other would be two announcements of one number.
    /// </summary>
    public string Number => Now.ToString("0.####", CultureInfo.InvariantCulture);
}
