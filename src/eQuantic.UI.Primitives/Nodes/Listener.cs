namespace eQuantic.UI.Primitives;

/// <summary>
/// Any subtree hears the raw pointer (#1562): Flutter's <c>Listener</c>. The child is what it wraps, and the
/// callbacks hear every pointer over it, in its own coordinates: a press (<see cref="OnPointerDown"/>), its
/// moves (<see cref="OnPointerMove"/>) and its end (<see cref="OnPointerUp"/>, or <see cref="OnPointerCancel"/>
/// when the system took it away), a mouse or a pen passing over with nothing pressed
/// (<see cref="OnPointerHover"/>), and a wheel or a trackpad (<see cref="OnPointerScroll"/>).
/// <para>
/// A pointer that went down on the listener is followed to its up or its cancel, wherever it moves: a drag that
/// leaves the box still reports, in the same coordinates. A listener only listens, as Flutter's does: a button
/// inside it still presses, a scroll view inside it still scrolls, and nothing it hears is taken from anything.
/// Where it hears is <see cref="Behavior"/>'s to say.
/// </para>
/// <para>
/// A block, not a member of <see cref="Pressable"/>: a list, a form or a chart hears the pointer as well as a
/// button does, and a recognizer of the app's own is written over it. Layout-transparent: the child owns every
/// visual. Listeners nest, and every one that hears a pointer is told, the innermost and topmost first.
/// </para>
/// </summary>
public sealed class Listener : SingleChildNode
{
    public override string NodeKind => "listener";

    public Listener(VisualNode child)
        : base(child)
    {
    }

    /// <summary>A button went down, or a finger or a pen touched, over the listener.</summary>
    public Action<PointerEvent>? OnPointerDown { get; init; }

    /// <summary>A pointer that went down on the listener moved, wherever it is now.</summary>
    public Action<PointerEvent>? OnPointerMove { get; init; }

    /// <summary>A pointer that went down on the listener lifted, wherever it is now.</summary>
    public Action<PointerEvent>? OnPointerUp { get; init; }

    /// <summary>The system took away a pointer that went down on the listener: nothing it was doing lands.</summary>
    public Action<PointerEvent>? OnPointerCancel { get; init; }

    /// <summary>A mouse or a pen moved over the listener with nothing pressed. A finger never hovers.</summary>
    public Action<PointerEvent>? OnPointerHover { get; init; }

    /// <summary>A wheel or a trackpad scrolled over the listener, by <see cref="PointerEvent.ScrollDelta"/>.</summary>
    public Action<PointerEvent>? OnPointerScroll { get; init; }

    /// <summary>Where the listener hears a pointer: only where its child is hit (the default), anywhere in its
    /// box hiding the listeners behind it, or anywhere in its box letting them hear too.</summary>
    public HitTestBehavior Behavior { get; init; } = HitTestBehavior.DeferToChild;

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
