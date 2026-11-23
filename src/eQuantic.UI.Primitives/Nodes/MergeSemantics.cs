namespace eQuantic.UI.Primitives;

/// <summary>
/// A SUBTREE A READER HEARS AS ONE STOP: what the stops inside it say, joined in reading order.
/// Layout-transparent and non-interactive, like <see cref="Group"/>: it draws nothing, takes no focus,
/// answers no key and adds no geometry. What it changes is how many times a reader stops.
///
/// <para>
/// When a control is among the stops (a checkbox, a switch, a button, a field, a tab, a link…), the one
/// stop IS that control: its role, its value and its states, and the bridge's activate presses it, so a
/// row that draws a switch beside its label is heard as the switch, named by the whole row. The control's
/// own name takes its place in the joined name. A second control stays a stop of its own, after the merged
/// one: one stop cannot be pressed as two things. With no control among them the stop is a
/// <see cref="SemanticRole.Group"/> named by what the subtree says, and nothing inside it is a stop.
/// </para>
///
/// <para>
/// What a reader passes over stays out of the name: a <see cref="Text"/> marked
/// <see cref="Text.ExcludeFromSemantics"/> and an <see cref="ExcludeSemantics"/> inside are not heard here
/// either.
/// </para>
///
/// <para>
/// The name is Flutter's, whose <c>MergeSemantics</c> merges a subtree into one node the same way. UIKit
/// says it by making a container one accessibility element and SwiftUI with
/// <c>accessibilityElement(children: .combine)</c>: three platforms that would have needed the word if the
/// web had never existed.
/// </para>
/// </summary>
public sealed class MergeSemantics : SingleChildNode
{
    public override string NodeKind => "mergeSemantics";

    public MergeSemantics(VisualNode child)
        : base(child)
    {
    }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
