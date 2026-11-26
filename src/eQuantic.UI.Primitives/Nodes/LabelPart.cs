namespace eQuantic.UI.Primitives;

/// <summary>
/// THE PART THAT NAMES ITS NEAREST NAMEABLE: what its subtree says is the name a reader hears for it, the label in this
/// vocabulary's sense, over any string the nameable carries. Its twin <see cref="DescriptionPart"/> describes instead.
/// Layout-transparent and non-interactive, like <see cref="Group"/>: it draws nothing, takes no focus, answers no key and
/// adds no geometry, and it is no stop of its own, so what it holds is read where it stands.
///
/// <para>
/// THE NAMEABLES. Containers that name themselves: a modal, open <see cref="Overlay"/>, an anchored panel whose
/// <see cref="Anchored.PanelRole"/> is <see cref="AnchorPanelRole.Dialog"/>, a <see cref="Group"/> and a
/// <see cref="LiveRegion"/>. Controls: a <see cref="TextEntry"/>, and a <see cref="Pressable"/> whose
/// <see cref="Pressable.Role"/> is a checkbox, a radio or a switch.
/// </para>
///
/// <para>
/// WHICH ONE IT REACHES. Walking up from the part, the first ancestor that is a nameable, or that holds a nameable
/// control outside the part that no nearer part already reached. So a field's label beside its entry names the entry, a
/// dialog's title names the dialog even when a field inside it is named by its own label, and a checkbox's label inside
/// it names the checkbox. Every <see cref="Overlay"/> and every anchored panel is a scope of its own: a part inside a
/// layer that is no dialog names nothing. An ancestor holding TWO such controls is refused rather than guessed, and the
/// realizer fails telling the developer to give each field its own group.
/// </para>
///
/// <para>
/// Several parts name one nameable together, in reading order, so a label and its affix read "Price €". A nameable with
/// a part is named by the part over its own string, which is the browser's order (<c>aria-labelledby</c> before
/// <c>aria-label</c>), and by its string when its parts say nothing.
/// </para>
///
/// <para>
/// The name is this vocabulary's own word. Flutter marks a dialog's title with <c>Semantics(namesRoute: true)</c>,
/// AppKit names a container by its title element and a field by its <c>accessibilityTitleUIElement</c>, Android a field
/// by the view whose <c>labelFor</c> points at it: four platforms that would need the node if the web had never existed.
/// </para>
/// </summary>
public sealed class LabelPart : SingleChildNode
{
    public override string NodeKind => "labelPart";

    public LabelPart(VisualNode child)
        : base(child)
    {
    }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
