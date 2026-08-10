namespace eQuantic.UI.Primitives;

/// <summary>
/// The interaction states a subtree can be told it is in.
/// <para>
/// Flags, because they combine: a control being pressed is usually also hovered, and a keyboard
/// user pressing Enter is focused and pressed at once.
/// </para>
/// <para>
/// Distinct from the (currently unused) <c>InteractionState</c> in ButtonStyles, which is exclusive
/// and includes Disabled and Loading — those are DECLARED on the component, not observed by the
/// host, and simulating them would be saying twice what the component already says once.
/// </para>
/// </summary>
[Flags]
public enum SimulatedState
{
    /// <summary>Whatever the pointer and focus actually say — the ordinary case.</summary>
    None = 0,

    /// <summary>The pointer is over it: the Box's <c>Hover</c> diff applies.</summary>
    Hovered = 1,

    /// <summary>It is being pressed: the pressed fill applies.</summary>
    Pressed = 2,

    /// <summary>It has keyboard focus: the focus ring and the <c>Focus</c> diff apply.</summary>
    Focused = 4,
}

/// <summary>
/// Draws its subtree AS IF it were hovered, pressed or focused, without anything having happened.
/// <para>
/// A pressed button cannot be handed to a constructor: pressed is something the host observes, not
/// something a tree states. So a documentation gallery, a design review and a visual-regression
/// suite could all show a control's rest state and nothing else — the three states a reader most
/// wants to compare were the three no page could draw.
/// </para>
/// <para>
/// One node rather than a property on every interactive component: it composes, it covers the
/// components that do not exist yet, and it works the same for a control nested three levels down
/// inside a card being previewed.
/// </para>
/// <para>
/// It changes only what is DRAWN. Nothing is invoked, no state is entered and no handler runs —
/// a simulated press is a picture of a press.
/// </para>
/// </summary>
public sealed class Simulated : VisualNode
{
    public override string NodeKind => "simulated";

    public Simulated(SimulatedState state, VisualNode child)
    {
        State = state;
        Child = child;
    }

    public SimulatedState State { get; init; }

    public VisualNode Child { get; init; }
}
