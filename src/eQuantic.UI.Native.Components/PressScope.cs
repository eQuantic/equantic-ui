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
        string? pressedPath = null, string? focusedPath = null,
        string? textPath = null, int caretIndex = 0, bool caretVisible = true,
        int selectionStart = 0, int selectionEnd = 0,
        Density density = Density.Comfortable,
        IReadOnlyList<string>? hoveredPaths = null)
    {
        Density = density;
        SelectionStart = selectionStart;
        SelectionEnd = selectionEnd;
        Pressed = pressed;
        Focused = focused;
        Hovered = hovered;
        HoveredPaths = hoveredPaths;
        PressedPath = pressedPath;
        FocusedPath = focusedPath;
        TextPath = textPath;
        CaretIndex = caretIndex;
        CaretVisible = caretVisible;
    }

    /// <summary>Where a scrollable path currently sits — the host's own store, read-only.</summary>
    public Func<string, float?>? ScrollOffset { get; init; }

    /// <summary>The IME composition in flight for the focused field ("" when none).</summary>
    public string MarkedText { get; init; } = "";

    public float? ScrollOffsetOf(string path) => ScrollOffset?.Invoke(path);

    /// <summary>The selected range in the field under edit (equal = no selection).</summary>
    public int SelectionStart { get; }
    public int SelectionEnd { get; }

    /// <summary>The field under edit, its caret, and whether the caret is in its ON blink.</summary>
    public string? TextPath { get; }
    public int CaretIndex { get; }
    public bool CaretVisible { get; }

    /// <summary>The target's density — what the hit-rect expansion answers to.</summary>
    public Density Density { get; }

    public Pressable? Pressed { get; }
    public Pressable? Focused { get; }

    /// <summary>Where the held press and the focus LIVE, which is what survives a rebuild. The
    /// node reference is kept alongside as the answer for a single frame that never rebuilt.</summary>
    public string? PressedPath { get; }
    public string? FocusedPath { get; }

    /// <summary>True when this node is the one being tracked: by path when there is one (the
    /// tree may have been rebuilt since), by reference otherwise.</summary>
    public bool IsTracked(LayoutNode node, VisualNode? tracked, string? trackedPath) =>
        trackedPath is { Length: > 0 }
            ? node.Path == trackedPath
            : tracked is not null && ReferenceEquals(node.Source, tracked);

    /// <summary>Spec S5: the node the pointer is over — its Box applies its Hover diff. Fed by
    /// the host's pointer tracking (the gesture slice); tests pass it directly.</summary>
    public VisualNode? Hovered { get; }

    /// <summary>Where the hover LIVES — the rebuild-surviving identities, exactly as
    /// <see cref="PressedPath"/> is for the press, and a CHAIN because CSS :hover matches every
    /// ancestor under the pointer (the web twin's semantics). Null = reference-only (tests).</summary>
    public IReadOnlyList<string>? HoveredPaths { get; }

    /// <summary>True when this node is under the pointer: by path membership in the hover
    /// chain when the host supplied one, by reference against the topmost otherwise.</summary>
    public bool IsHovered(LayoutNode node, VisualNode? candidate)
    {
        if (HoveredPaths is { Count: > 0 } chain)
            return node.Path is { } path && chain.Contains(path);
        return candidate is not null && ReferenceEquals(Hovered, candidate);
    }
    /// <summary>
    /// The states this subtree is being DRAWN as, set by a <see cref="Simulated"/> node while
    /// its child is emitted and restored after. Not part of the host's tracking: nothing was
    /// pressed, the picture just says it was.
    /// </summary>
    public SimulatedState Simulated { get; set; }

    /// <summary>The surface an <see cref="Primitives.InView"/> is measured against, and what it
    /// last answered — the node is rebuilt every frame and cannot remember for itself.</summary>
    public Rect Surface { get; set; }

    public InViewStore? InView { get; init; }

    public ColorToken? PendingFill { get; set; }
    public bool PendingFocusRing { get; set; }

    /// <summary>
    /// True while the subtree of the PRESSED control is drawn, set by its <see cref="Pressable"/>
    /// and restored after, the way a <see cref="Simulated"/> node sets its states: a press is the
    /// control's, and every box inside it shows its <c>Pressed</c> diff (#508). The web twin is
    /// <c>.eq-pressable:active .cls</c>.
    /// </summary>
    public bool InPressedControl { get; set; }

    /// <summary>True while the subtree of the FOCUSED control is drawn — its boxes show their
    /// <c>Focus</c> diff, as <see cref="InPressedControl"/>'s show their press.</summary>
    public bool InFocusedControl { get; set; }

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
