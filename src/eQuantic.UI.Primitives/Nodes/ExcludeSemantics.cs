namespace eQuantic.UI.Primitives;

/// <summary>
/// A SUBTREE A READER PASSES OVER: drawn for the eye and heard by no one. Layout-transparent and
/// non-interactive, like <see cref="Group"/>: it draws nothing of its own, takes no focus, answers no key
/// and adds no geometry. Every bridge leaves what it holds out of what a reader hears, the web's
/// accessibility tree and Photon's alike.
///
/// <para>
/// It is for what another stop already says, or what says nothing at all: the initials an avatar draws
/// beside the name a row reads, a chart's drawn figures when a table beside it holds the numbers, the
/// ornament a design puts around a heading. Wrap no control in it. The subtree is passed over whatever it
/// holds, as Flutter's is and as the web's <c>aria-hidden</c> is, so a control inside would still take a
/// press and a Tab while no reader could tell anyone it is there.
/// </para>
///
/// <para>
/// <see cref="Excluding"/> says whether it excludes right now, so a subtree that is heard in one state and
/// not in another keeps its shape, and everything inside it keeps its state, when that changes.
/// </para>
///
/// <para>
/// The name is Flutter's <c>ExcludeSemantics</c>. UIKit says it with
/// <c>accessibilityElementsHidden</c>, Android with <c>IMPORTANT_FOR_ACCESSIBILITY_NO_HIDE_DESCENDANTS</c>
/// and SwiftUI with <c>accessibilityHidden</c>: platforms that would have needed the word if the web had
/// never existed.
/// </para>
/// </summary>
public sealed class ExcludeSemantics : SingleChildNode
{
    public override string NodeKind => "excludeSemantics";

    public ExcludeSemantics(VisualNode child)
        : base(child)
    {
    }

    /// <summary>Whether the subtree is passed over now. True by default: false keeps the node and reads
    /// what it holds as if it were not there.</summary>
    public bool Excluding { get; init; } = true;

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
