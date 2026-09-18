using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// What a screen reader says about each word of the vocabulary — the first of the six dispatches to
/// leave its switch and answer to the compiler instead.
///
/// <para>
/// The walk this replaces was a <c>switch</c> over <c>LayoutNode.Source</c> with no default arm, so a
/// node added to the vocabulary joined the silent fall-through and announced nothing. That is not a
/// hypothesis: <c>Image</c>, <c>Canvas</c>, <c>Vector</c>, <c>Drawing</c> and <c>CameraPreview</c> all
/// carried a <c>Label</c> the web had honoured for months and Photon read for none of them, and each
/// was found by hand. Here the same omission does not compile.
/// </para>
///
/// <para>
/// THE RESULT IS "KEEP WALKING". A node that announces CONSUMES its subtree — a control's inner text
/// IS its name, exactly as the web's <c>&lt;button&gt;text&lt;/button&gt;</c> reads as one element —
/// and everything else hands the question down to its children. The recursion stays in
/// <see cref="SemanticsTree"/>, which owns the frame; the visitor owns only what each word means.
/// </para>
///
/// <para>
/// A DECLINE IS A CONSTANT NAMED FOR ITS REASON. The exemption list of <c>VocabularyCoverageTests</c>
/// carried those reasons in a comment beside a string; they are here now, where the compiler sees the
/// node and a reviewer sees why. FOURTEEN nodes announce, twenty-seven decline, and the
/// twenty-seven are not one kind of silence: pure layout, a wrapper, ornament, an escape hatch that
/// cannot cross, the expansion seam, and two that are a real gap wearing an exemption.
/// <para>
/// "Announce" means CAN announce, which is why it is not the same as the arms whose body says
/// <c>Announce</c>: seven do so unconditionally, and seven more only when there is something to say
/// — the six <c>Graphic</c> arms when the graphic is labelled, and <see cref="Text"/> when it has
/// content. Counting only the unconditional seven is the mistake this paragraph was once rewritten
/// to make.
/// </para>
/// </para>
/// </summary>
internal sealed partial class SemanticsVisitor(List<SemanticNode> nodes)
    : IVisualNodeVisitor<LayoutNode, bool>
{
    /// <summary>The frame's semantic nodes, in reading order — fixed for the pass, so it lives here
    /// rather than travelling down with each visit.</summary>
    private readonly List<SemanticNode> _nodes = nodes;

    // ---- the two answers ---------------------------------------------------------------------

    /// <summary>Keep walking: this node said nothing of its own, and its children speak for
    /// themselves.</summary>
    private const bool Descend = true;

    /// <summary>Stop here: this node announced, and everything under it is part of what it says.
    /// The same rule the focus route applies (<c>InputSink.WithoutFocusStops</c>), so a walk and a
    /// Tab reach the same stops — which is what <c>SemanticsMatchFocusStops</c> holds.</summary>
    private const bool Consumed = false;

    // ---- the reasons a node declines ----------------------------------------------------------

    /// <summary>Pure layout: a Row is not an object to a screen reader. The engine resolved it into
    /// geometry, and geometry announces nothing — the children inside it do.</summary>
    private const bool PureLayout = Descend;

    /// <summary>Pure behaviour: a Shortcut, a Hoverable or a Simulated wraps a child without being
    /// anything itself. What a screen reader lands on is what is inside.</summary>
    private const bool Wraps = Descend;

    /// <summary>Decorative BY AGREEMENT, and this is where that agreement is written down. The web
    /// marks a spinner and a looping animation <c>aria-hidden</c>, on the reasoning that a busy
    /// indicator is ornament and the surrounding copy announces the wait; Photon reached the same
    /// answer by having no case at all. Until this line, nothing recorded that the two were supposed
    /// to agree.</summary>
    private const bool Decorative = Descend;

    /// <summary>The DOM escape hatch, which cannot cross to a GPU surface at all — the same reason
    /// the layout engine exempts it. Nothing here is a judgement about accessibility; there is no
    /// node on this target to announce.</summary>
    private const bool EscapeHatch = Descend;

    /// <summary>
    /// A REAL GAP WEARING AN EXEMPTION, and the only two declines here that should not be declines.
    /// The web honours both (<c>WebRealizer.LowerNavigable</c> and <c>LowerOverlay</c>) and Photon is
    /// silent, because every announcement consumes its subtree and doing that to a navigable grid
    /// would hide every row inside it. What they need is a role meaning "a labelled group, keep
    /// walking", and <see cref="SemanticRole"/> has none: it is ten leaf roles. That is a vocabulary
    /// decision with a bridge per platform behind it —
    /// <see href="https://github.com/eQuantic/equantic-ui/issues/187">#187</see>, which turns both of
    /// these into announcements the day it is answered.
    /// </summary>
    private const bool AwaitsGroupRole = Descend;

    /// <summary>The expansion seam. A component is not a thing on screen; it BUILT the things on
    /// screen, and the layout pass keeps it in the tree as the parent of what it built
    /// (<c>MeasureComponent</c>). Walking through it is how the built tree is reached.</summary>
    private const bool Seam = Descend;

    // ---- the seam ------------------------------------------------------------------------------

    /// <inheritdoc cref="Seam"/>
    public bool Visit(UiComponent node, LayoutNode laidOut) => Seam;

    // ---- announcing ----------------------------------------------------------------------------

    /// <summary>Add one stop and consume the subtree.</summary>
    private bool Announce(SemanticNode node)
    {
        _nodes.Add(node);
        return Consumed;
    }
}
