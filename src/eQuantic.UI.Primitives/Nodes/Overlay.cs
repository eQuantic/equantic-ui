namespace eQuantic.UI.Primitives;

/// <summary>
/// The VIEWPORT LAYER (Phase C infrastructure): the child escapes the page flow and realizes
/// against the viewport, painted ABOVE everything in the page pass — web lowers to a generated
/// fixed inset-0 layer, native defers the subtree to an overlay pass after the page (painter's
/// order) and lays it out against the viewport. The child owns its own composition (scrim,
/// centering, sheets) from the ordinary vocabulary — Overlay is only the layer. Declarative:
/// presence in the build shows it (`if (_confirming) … new Overlay(…)`), state removes it.
/// </summary>
public sealed class Overlay : SingleChildNode
{
    public override string NodeKind => "overlay";

    public Overlay(VisualNode child)
        : base(child)
    {
    }


    /// <summary>False = a NON-MODAL layer (toasts): pointer input passes through everywhere except
    /// the layer's own pressables. Native is passthrough by construction (only registered regions
    /// hit); the web realizer lowers the pointer-events variant. Default TRUE (dialogs, sheets).</summary>
    public bool Modal { get; init; } = true;

    /// <summary>The dialog's accessible NAME — what a screen reader says on entry, before reading
    /// the content. A Dialog passes its title; a modal layer without one is announced as just
    /// "dialog", which tells the user a wall appeared but not which.</summary>
    public string? Label { get; init; }

    /// <summary>An ALERT dialog — a confirmation that interrupts (the destructive confirm).
    /// Lowers to <c>role="alertdialog"</c>, which assistive tech announces assertively instead of
    /// politely; everything else about the layer (trap, modal, restore) is identical.</summary>
    public bool Alert { get; init; }

    /// <summary>Visibility while <see cref="Motion"/> is set — a CLOSED layer stays mounted and
    /// animates out. Ignored without Motion (the caller renders the Overlay conditionally).</summary>
    public bool Open { get; init; } = true;

    /// <summary>
    /// OPEN/CLOSE motion (the drawer and command-palette contract, the <see cref="Anchored.Motion"/>
    /// twin): the layer stays MOUNTED across both states — so element identity survives and the
    /// transition actually runs — and fades between them; closed ends <c>visibility:hidden</c>, out
    /// of hit-testing and the focus order. <c>null</c> = mount/unmount (the historical snap). Keep
    /// building the layer's content while closed: an emptied layer collapses mid-fade.
    /// Photon fence: the overlay animator (native snaps until it lands).
    /// </summary>
    public TransitionSpec? Motion { get; init; }

    // WEB FENCE (no portal, v1): the layer is `position: fixed` where it sits in the tree, so an
    // ANCESTOR that creates a stacking context (a Stack cell's paint-order z-index, a transform, a
    // filter) traps it — the layer then stacks inside that ancestor instead of over the page. Keep
    // Overlays out of such subtrees (a Column is layout-neutral and safe) until they portal to the
    // document root. Native has no such rule: overlay layers queue against the viewport.

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
