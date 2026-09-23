namespace eQuantic.UI.Code;

/// <summary>
/// A stretch of document between two positions. <see cref="Anchor"/> is where the selection began
/// and <see cref="Focus"/> is where the caret is now — the pair is DIRECTED, because shift+arrow
/// has to know which end it is dragging. <see cref="Start"/>/<see cref="End"/> give the ordered
/// view everything else wants.
/// </summary>
public readonly record struct CodeRange(CodePosition Anchor, CodePosition Focus)
{
    public CodeRange(CodePosition caret) : this(caret, caret) { }

    public CodePosition Start => Anchor <= Focus ? Anchor : Focus;
    public CodePosition End => Anchor <= Focus ? Focus : Anchor;

    /// <summary>A caret rather than a selection — nothing is covered.</summary>
    public bool IsEmpty => Anchor == Focus;

    /// <summary>Collapses to the caret end, the way typing over a selection does.</summary>
    public CodeRange Collapsed() => new(Focus, Focus);
}
