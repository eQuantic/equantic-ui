using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>Carries the held press through the emit walk: entering the pressed Pressable arms the
/// token swap, and the FIRST descendant Box consumes it (the component convention: Pressable → Box
/// carries the fill — the spec's "token swap on the same rrect").</summary>
/// <para>Top-level rather than nested since S6, for the reason <see cref="MotionScope"/> is: the walk
/// that threads it is <c>EmitVisitor</c>'s now. ONE PER LAYER — <see cref="PhotonRealizer"/> builds a fresh
/// scope for the page and for each overlay, because the fields below are pushed and restored down a
/// subtree and a leftover must not cross between layers.</para>
internal sealed class PressScope
{
    public PressScope(Pressable? pressed, Pressable? focused, VisualNode? hovered = null,
        Element? pressedAt = null, int focusedId = 0,
        Element? textAt = null, int caretIndex = 0, bool caretVisible = true,
        int selectionStart = 0, int selectionEnd = 0,
        Density density = Density.Comfortable,
        IReadOnlyList<Element>? hoveredAt = null)
    {
        Density = density;
        SelectionStart = selectionStart;
        SelectionEnd = selectionEnd;
        Pressed = pressed;
        Focused = focused;
        Hovered = hovered;
        HoveredAt = hoveredAt;
        PressedAt = pressedAt;
        FocusedId = focusedId;
        TextAt = textAt;
        CaretIndex = caretIndex;
        CaretVisible = caretVisible;
    }

    /// <summary>The IME composition in flight for the focused field ("" when none).</summary>
    public string MarkedText { get; init; } = "";

    /// <summary>The id of the input shape of a <see cref="Drawing"/> under the pointer, which draws its
    /// Hover, or zero. A shape is a part of its drawing's position rather than a position of its own,
    /// so the hover chain, which holds positions, cannot name it (#674).</summary>
    public int HoveredPart { get; init; }

    /// <summary>A phone keyboard's composition in the focused field, as a range of the text it was
    /// composed in, or null (see <see cref="TextEditingValue"/>). Drawn only over that text.</summary>
    public (int Start, int End, string Text)? Composing { get; init; }

    /// <summary>The selected range in the field under edit (equal = no selection).</summary>
    public int SelectionStart { get; }
    public int SelectionEnd { get; }

    /// <summary>The field under edit, its caret, and whether the caret is in its ON blink.</summary>
    public Element? TextAt { get; }
    public int CaretIndex { get; }
    public bool CaretVisible { get; }

    /// <summary>The target's density — what the hit-rect expansion answers to.</summary>
    public Density Density { get; }

    public Pressable? Pressed { get; }
    public Pressable? Focused { get; }

    /// <summary>Where the held press LIVES, its control's position, which is what survives a rebuild,
    /// and the id the focus is held by (zero for none). The node reference is kept alongside as the
    /// answer for a single frame that never rebuilt.</summary>
    public Element? PressedAt { get; }
    public int FocusedId { get; }

    /// <summary>True when this node is the pressed control: by its position when the host tracks one
    /// (the tree may have been rebuilt since), by reference otherwise.</summary>
    public bool IsPressed(LayoutNode node) =>
        PressedAt is not null
            ? ReferenceEquals(node.Element, PressedAt)
            : Pressed is not null && ReferenceEquals(node.Source, Pressed);

    /// <summary>True when this node is the focused control: by its position's id when the host tracks
    /// one, by reference otherwise.</summary>
    public bool IsFocused(LayoutNode node) =>
        FocusedId != 0
            ? node.Element is { } at && at.Id == FocusedId
            : Focused is not null && ReferenceEquals(node.Source, Focused);

    /// <summary>Spec S5: the node the pointer is over — its Box applies its Hover diff. Fed by
    /// the host's pointer tracking (the gesture slice); tests pass it directly.</summary>
    public VisualNode? Hovered { get; }

    /// <summary>Where the hover LIVES — the positions under the pointer, which survive a rebuild
    /// exactly as <see cref="PressedAt"/> does for the press, and a CHAIN because CSS :hover matches
    /// every ancestor under the pointer (the web twin's semantics). Null = reference-only (tests).</summary>
    public IReadOnlyList<Element>? HoveredAt { get; }

    /// <summary>True when this node is under the pointer: by its position's place in the hover chain
    /// when the host supplied one, by reference against the topmost otherwise.</summary>
    public bool IsHovered(LayoutNode node, VisualNode? candidate)
    {
        if (HoveredAt is { Count: > 0 } chain)
        {
            if (node.Element is not { } at) return false;
            for (var i = 0; i < chain.Count; i++)
                if (ReferenceEquals(chain[i], at)) return true;
            return false;
        }
        return candidate is not null && ReferenceEquals(Hovered, candidate);
    }
    /// <summary>
    /// The states this subtree is being DRAWN as, set by a <see cref="Simulated"/> node while
    /// its child is emitted and restored after. Not part of the host's tracking: nothing was
    /// pressed, the picture just says it was.
    /// </summary>
    public SimulatedState Simulated { get; set; }

    /// <summary>The surface an <see cref="Primitives.InView"/> is measured against.</summary>
    public Rect Surface { get; set; }

    /// <summary>Whether an <see cref="Primitives.InView"/> reports in this frame: only in a tree that
    /// is drawn again, a frame realized with a live owner (see <see cref="InViewState"/>).</summary>
    public bool ReportsInView { get; init; }

    public bool PendingFocusRing { get; set; }

    /// <summary>
    /// True while the subtree of the PRESSED control is drawn, set by its <see cref="Pressable"/>
    /// and restored after, the way a <see cref="Simulated"/> node sets its states: a press is the
    /// control's, and every box inside it shows its <c>Pressed</c> diff (#508), up to a control
    /// nested in it, which sets its own (#616). The web twin is the pressed family's selector.
    /// </summary>
    public bool InPressedControl { get; set; }

    /// <summary>True while the subtree of the FOCUSED control is drawn — its boxes show their
    /// <c>Focus</c> diff, as <see cref="InPressedControl"/>'s show their press.</summary>
    public bool InFocusedControl { get; set; }

    /// <summary>Whether the nearest control around the box being drawn is hovered: null outside
    /// every control and inside a disabled one, where a box's hover is its own; true or false inside
    /// an enabled one, whose hover its boxes take (#614), up to a control nested in it, which sets
    /// its own.</summary>
    public bool? ControlHovered { get; set; }

    /// <summary>True while an enabled control's subtree is drawn under a <see cref="Simulated"/>
    /// node: a control nested in it is another control, and the picture's press and focus stop at
    /// it, as the real ones do (#616). A <see cref="Simulated"/> node starts it over.</summary>
    public bool InControl { get; set; }

    /// <summary>True while a DISABLED control's subtree is drawn: its boxes show no focus and no
    /// press, pictured or real, whatever control around it is in that state (#508). The web twin
    /// lowers no focus or press rule for them.</summary>
    public bool InDisabledControl { get; set; }

    /// <summary>
    /// True while the subtree of a vertical <see cref="ScrollView"/> scrolled past
    /// <see cref="Pinned.ScrolledThreshold"/> is drawn: a pinned header in it draws its
    /// <c>ScrolledStyle</c> (#506). The nearest scroll view decides, and outside every one the page
    /// does not scroll on Photon. The web twin is the header's <c>data-eq-scrolled</c>.
    /// </summary>
    public bool SurfaceScrolled { get; set; }
}
