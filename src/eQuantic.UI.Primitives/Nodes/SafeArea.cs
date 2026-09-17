namespace eQuantic.UI.Primitives;

/// <summary>
/// Keeps its child clear of the parts of the display the SYSTEM owns — the notch, the status bar,
/// the home indicator, a desktop title bar. The insets are the HOST's to report, never the app's to
/// guess: web reads <c>env(safe-area-inset-*)</c>, which the browser fills, and Photon reads them
/// from the window (zero on a desktop with no cutouts, which is the correct answer there).
/// <para>
/// It pads rather than positions, so it composes: a scroll view inside one scrolls under nothing,
/// and a bottom bar inside one sits above the home indicator instead of under it.
/// </para>
/// </summary>
public sealed class SafeArea : SingleChildNode
{
    public override string NodeKind => "safeArea";

    public SafeArea(VisualNode child, SafeEdges edges = SafeEdges.All)
        : base(child)
    {
        Edges = edges;
    }

    public SafeEdges Edges { get; init; }

    /// <summary>Added to whatever the host reports — a bar's own padding on top of the inset.</summary>
    public EdgeInsets Extra { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
