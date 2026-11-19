namespace eQuantic.UI.Primitives;

/// <summary>
/// What a person can ask the menu of (#1531): the child is the thing, and <see cref="OnRequested"/> hears the
/// request, a secondary click (the right button, or a Control-click on a Mac), a finger held still on it, or the
/// keyboard's menu key or Shift+F10 while the focus is inside it. The app answers by opening its own menu,
/// usually an <see cref="Anchored"/> placed <see cref="Anchored.At"/> the request's point, which is what the
/// <c>ContextMenu</c> component composes.
/// <para>
/// A block, not a member of <see cref="Pressable"/>: a tree's row, a paragraph, a chart or a canvas asks for a
/// menu as well as a button does. Layout-transparent: the child owns every visual. Targets nest, and the
/// innermost one under the pointer or around the focus answers alone.
/// </para>
/// </summary>
public sealed class ContextTarget : SingleChildNode
{
    public override string NodeKind => "contextTarget";

    /// <summary>How long a finger holds a target still to ask for its menu, in milliseconds: the platforms' long
    /// press. A finger that travels first is scrolling. The browser's controller holds the same figure.</summary>
    public const float HoldMs = 500;

    public ContextTarget(VisualNode child, Action<ContextRequest> onRequested)
        : base(child)
    {
        OnRequested = onRequested;
    }

    /// <summary>Where the menu was asked for, in this target's own box, or nowhere when the keyboard asked.</summary>
    public Action<ContextRequest> OnRequested { get; init; }

    /// <summary>Whether the target answers. A disabled one answers nothing, and the request goes to no target
    /// around it either.</summary>
    public bool Enabled { get; init; } = true;

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
