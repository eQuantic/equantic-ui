using eQuantic.UI.Primitives;

namespace eQuantic.UI.Email;

/// <summary>
/// What an email may contain, asked of the vocabulary once and answered by BOTH alternatives.
///
/// <para>
/// A message has two bodies — the HTML (<see cref="EmailVisitor"/>) and the <c>text/plain</c>
/// (<see cref="EmailTextVisitor"/>) — built from the same tree so they cannot drift. Seven words
/// cross into that medium: <see cref="Column"/>, <see cref="Row"/>, <see cref="Text"/>,
/// <see cref="Box"/>, <see cref="Image"/>, <see cref="Link"/>, and the component seam. They are
/// abstract here, because each alternative writes them differently. The other THIRTY-FOUR are
/// refused, and refused IDENTICALLY — one <see cref="Refuse"/>, inherited by both.
/// </para>
///
/// <para>
/// THAT SHARING IS THE POINT, and it closes a real divergence. The HTML walker always had a loud
/// default arm; the plain-text walker had NO default at all, so a node neither of them supports
/// fell out of its switch in silence and the two parts of the same message disagreed about what the
/// tree meant. Here there is one list, one reason, and no arm to fall out of.
/// </para>
///
/// <para>
/// The refusal is not a gap to be closed later. An email is a printed page that happens to have
/// links: Outlook renders with Word's engine, so scrolling, pressing and dragging are not SMALLER
/// in this medium, they are nothing. A node leaving the refused set means the medium learned
/// something, which is why the set is written out rather than caught by a default.
/// </para>
/// </summary>
internal abstract partial class EmailWalk : IVisualNodeVisitor<Nothing, Nothing>
{
    /// <summary>
    /// A shared component expands here — via <c>Build</c>, NOT the web's <c>BuildContained</c>, and
    /// the divergence is deliberate: BuildContained catches a component's throw and renders a
    /// describe-box in its place, which is right on a live page a developer is looking at and wrong
    /// in a message about to be SENT to a reader. In email, a broken component must fail the send,
    /// never reach an inbox dressed as content.
    /// <para>
    /// THE BOUND IS NOT PART OF THE DIVERGENCE, and for a while it was taken to be. An implementor
    /// opens <c>ComponentBoundary.Enter</c> around the expansion, which shares the one depth counter
    /// with the containing realizers and THROWS where they contain — catchable, so the render fails
    /// and nothing is sent. Without it a component that builds itself walked back into Build forever
    /// and died on a <c>StackOverflowException</c>: uncatchable, so the sending process went with it
    /// and reported nothing at all, which is the one outcome worse than a failed send.
    /// </para>
    /// </summary>
    public abstract Nothing Visit(UiComponent node, Nothing state);

    /// <summary>
    /// The one refusal, loud and naming the node — the repo's rule: never a silent divergence. The
    /// alternative is a message that goes out without a piece of itself and says nothing about it.
    /// </summary>
    private protected static Nothing Refuse(VisualNode node) =>
        throw new NotSupportedException(
            $"A {node.GetType().Name} cannot be realized in an email: the medium has no "
            + "scrolling, no interaction and no script. Compose the message from Column, "
            + "Row, Text, Box, Image and Link.");
}
