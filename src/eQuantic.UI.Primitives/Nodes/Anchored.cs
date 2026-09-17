namespace eQuantic.UI.Primitives;

/// <summary>
/// The ANCHORED overlay (wave 3): a floating <see cref="Panel"/> positioned relative to the
/// in-flow <see cref="Anchor"/> — the primitive under menus, selects and popovers. The anchor owns
/// layout (the node is layout-transparent); the panel exists only while <see cref="Open"/> and
/// paints ABOVE the page. When <see cref="OnDismiss"/> is set, an invisible viewport scrim behind
/// the panel consumes the outside tap and fires it (tap-outside-closes). Web realizes as a
/// position:relative host with an absolute panel — pure CSS, no JS positioning, SSR-exact; native
/// queues a synthetic overlay layer with the panel <c>Positioned</c> from the anchor's absolute
/// bounds. A dismissible OPEN panel declares Escape through the shortcut pipeline (close = the
/// same <see cref="OnDismiss"/> the scrim fires), and a hover-revealed one is Esc-SUPPRESSED by
/// the runtime controller (WCAG 1.4.13). v1 fences: viewport flip/clamp (a panel near the edge
/// does not reposition), escape from clipping ancestors on web (keep anchors out of
/// overflow-hidden scrollers).
/// </summary>
public sealed class Anchored : VisualNode
{
    public override string NodeKind => "anchored";

    /// <summary>Default distance (dp) between the anchor's edge and the panel.</summary>
    public const float DefaultGap = 4;

    public Anchored(VisualNode anchor, VisualNode panel)
    {
        Anchor = anchor;
        Panel = panel;
    }

    public VisualNode Anchor { get; init; }

    /// <summary>The floating content. Built unconditionally so the tree shape is stable across
    /// open/close (reconciler identity); realizers emit it only while <see cref="Open"/>.</summary>
    public VisualNode Panel { get; init; }

    public AnchorPlacement Placement { get; init; } = AnchorPlacement.BottomStart;

    /// <summary>Distance (dp) between anchor and panel on the placement axis.</summary>
    public float Gap { get; init; } = DefaultGap;

    /// <summary>Controlled visibility — components own the state (Menu/Select internally,
    /// Popover through its caller).</summary>
    public bool Open { get; init; }

    /// <summary>Fired by the invisible outside-tap scrim; null = no scrim (panel only closes
    /// through its own actions).</summary>
    public Action? OnDismiss { get; init; }

    /// <summary>The panel refuses to be narrower than the anchor (the Select contract) —
    /// CSS <c>min-width:100%</c> of the host; native wraps the panel in a MinWidth box.</summary>
    public bool MatchAnchorWidth { get; init; }

    /// <summary>Wave 3b (the Tooltip mechanism): the panel shows while the POINTER hovers the
    /// anchor — pure CSS on web (the generated .eq-hoverreveal rules, zero JS), the host's hover
    /// pipeline on native. Keyboard focus reveals it the same way (:has(:focus-visible) — focus
    /// only, because a tooltip popping on every mouse CLICK is in the way). No scrim (hover
    /// leaves = closed); never fires on touch. Composes with <see cref="Open"/> (either shows
    /// the panel).</summary>
    public bool OpenOnHover { get; init; }

    /// <summary>
    /// What the floating panel IS to assistive tech. <see cref="AnchorPanelRole.Menu"/> and
    /// <see cref="AnchorPanelRole.Listbox"/> make the panel a real <c>role="menu"</c> /
    /// <c>role="listbox"</c> with a deterministic id, number its item rows (the descendants whose
    /// <see cref="PressableRole"/> says MenuItem/Option), and teach the ANCHOR its side of the
    /// pattern: <c>aria-haspopup</c>, <c>aria-controls</c> while open — and for a Listbox the
    /// anchor becomes the <c>combobox</c>, which is what a Select's field is.
    /// </summary>
    public AnchorPanelRole PanelRole { get; init; }

    /// <summary>
    /// Which item row the KEYBOARD is on, counted over the panel's item rows in tree order —
    /// <c>-1</c> = none. Lowered as <c>aria-activedescendant</c> on the anchor: the highlight the
    /// arrows move is otherwise only PAINT, and a screen reader following the keyboard hears
    /// nothing move. The focus itself stays on the trigger, which is the combobox pattern's own
    /// arrangement.
    /// </summary>
    public int ActiveIndex { get; init; } = -1;

    /// <summary>
    /// The panel DESCRIBES the anchor — the Tooltip contract. The web panel becomes
    /// <c>role="tooltip"</c> with a deterministic id (FNV of its text, the `.eq-desc` rule, so SSR
    /// and hydration agree), and the anchor's root carries <c>aria-describedby</c> pointing at it:
    /// a screen reader reads the hint after the control's own name, whether or not the panel is
    /// visually revealed. Only meaningful with <see cref="OpenOnHover"/>; a menu's panel is not a
    /// description of its trigger.
    /// </summary>
    public bool DescribesAnchor { get; init; }

    /// <summary>
    /// Visual style for the outside-tap scrim (mega-menu page dimming): when set (and
    /// <see cref="OnDismiss"/> is wired), the viewport-covering scrim PAINTS instead of being
    /// invisible — background/gradient/backdrop-blur compose exactly like a Box. Ignored for
    /// hover-open panels (they have no scrim). <c>null</c> = the historical invisible scrim.
    /// </summary>
    public BoxStyle? ScrimStyle { get; init; }

    /// <summary>The open/close lift distance (dp) when <see cref="Motion"/> is set — a nudge
    /// toward the anchor, not a journey (spec §06's Presence rule, halved).</summary>
    public const float MotionLiftDp = 8;

    /// <summary>
    /// OPEN/CLOSE motion (the mega-menu contract): when set, the panel and the scrim stay MOUNTED
    /// across open/close — the tree keeps its shape, so both states share element identity — and
    /// visibility glides per this spec: the panel fades while lifting <see cref="MotionLiftDp"/>
    /// toward the anchor, the scrim cross-fades alongside. A closed panel ends
    /// <c>visibility:hidden</c> — outside hit-testing and the focus order (pure CSS: opacity/
    /// transform/visibility transitions; no JS). Photon fence: the overlay animator (panels keep
    /// snapping until it lands). <c>null</c> = mount/unmount (the historical snap). Keep building
    /// the LAST panel content while closed — an emptied panel would collapse mid-fade.
    /// </summary>
    public TransitionSpec? Motion { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
