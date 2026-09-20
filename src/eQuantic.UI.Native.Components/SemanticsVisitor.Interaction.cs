using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Interaction and motion — fifteen words. Three are controls and announce as one stop each; a
/// fourth, <see cref="Progress"/>, announces WITHOUT being one — it is read, never moved, which is
/// why it carries no tab stop and no key handler; two, <see cref="Navigable"/> and
/// <see cref="LiveRegion"/>, are GROUPS that announce and then keep walking; the remaining nine wrap
/// a child or are ornament.
/// <para>
/// This tally said "the fourteenth, Navigable, is the gap" until <see cref="SemanticRole.Group"/>
/// landed — the running-count shape that rots, caught here one commit after the gap closed.
/// </para>
/// </summary>
internal sealed partial class SemanticsVisitor
{
    /// <summary>
    /// A control's inner text IS its name, not a separate stop — the subtree is consumed, exactly as
    /// the web's <c>&lt;button&gt;text&lt;/button&gt;</c> reads as one element.
    /// </summary>
    public bool Visit(Pressable node, LayoutNode laidOut)
    {
        // A check states its state beside the name, never inside it — the exact mirror of the web's
        // aria-checked. Mixed is checkbox-only, ARIA's own rule.
        var (role, check) = node.Role switch
        {
            PressableRole.Checkbox => (SemanticRole.Checkbox,
                (SemanticCheck?)(node.Mixed ? SemanticCheck.Mixed
                    : node.Selected == true ? SemanticCheck.On : SemanticCheck.Off)),
            PressableRole.Switch => (SemanticRole.Switch,
                (SemanticCheck?)(node.Selected == true ? SemanticCheck.On : SemanticCheck.Off)),
            PressableRole.GridCell => (SemanticRole.GridCell, (SemanticCheck?)null),
            _ => (SemanticRole.Button, null),
        };
        // PICKED-ness, for the three roles that have it. Before the Selected field existed a Tab and
        // an Option arrived here as plain Buttons whose selection was paint only.
        bool? selected = node.Role is PressableRole.Tab or PressableRole.Option or PressableRole.GridCell
            ? node.Selected == true
            : null;
        return Announce(new(role, laidOut.Path ?? "", laidOut.Bounds,
            node.Label ?? TextWithin(laidOut), null, node.Disabled, check,
            node.Expanded,
            node.Role == PressableRole.Destination && node.Selected == true,
            selected));
    }

    /// <summary>One stop, named by the author or derived from the words inside it — the same rule
    /// the Pressable arm follows, and the reason both consume their subtree.</summary>
    public bool Visit(Link node, LayoutNode laidOut) =>
        Announce(new(SemanticRole.Link, laidOut.Path ?? "", laidOut.Bounds,
            node.Label ?? TextWithin(laidOut), null, false, Current: node.Current));

    /// <summary>
    /// One stop for the whole control, inner pressables stay pointer-only — the same rule the focus
    /// route applies (<c>InputSink.WithoutFocusStops</c>).
    /// <para>
    /// The VALUE rides the same slot a text field's does (spec C7): the bridges report an Adjustable
    /// as their platform's slider, and a slider whose value is null announces its name and nothing
    /// else — the native half of the invalid <c>role="slider"</c> the web emitted.
    /// </para>
    /// <para>
    /// The value is the SLIDER role's and no other's, exactly as on the web. Nothing here needs ARIA
    /// to say so — the bridges report all three roles as one — but the node's own contract does, and
    /// a contract that holds on one target is not a contract. This became reachable the moment
    /// <c>UI.Adjustable</c> grew a value argument beside a role one: the same tree would have
    /// announced a position on Photon and none in the DOM.
    /// </para>
    /// </summary>
    public bool Visit(Adjustable node, LayoutNode laidOut) =>
        Announce(new(SemanticRole.Slider, laidOut.Path ?? "", laidOut.Bounds,
            node.Label,
            node.Role == AdjustableRole.Slider ? node.Spoken : null,
            false,
            // The NUMBERS travel beside the words now, so a bridge can offer its platform's own
            // range — a `RangeInfo` on Android, `AXMinValue`/`AXMaxValue` on macOS. They ride under
            // the same condition as the words: a node that is not announced as a slider is not
            // announced as having a range either (#243).
            Range: node.Role == AdjustableRole.Slider ? node.Value : null));

    /// <summary>
    /// Spec B14: the bar says WHAT IT IS FOR and HOW FAR ALONG. It is not a Slider — the platforms
    /// split the two (AXProgressIndicator, android.widget.ProgressBar), and calling this one a
    /// slider would offer VoiceOver's adjust gestures on something nothing can move.
    /// <para>
    /// A null value is INDETERMINATE and announces the name alone, which is the honest answer when
    /// nothing knows how far along it is — the opposite of the Adjustable rule, where a missing
    /// value means the node was never a slider.
    /// </para>
    /// </summary>
    public bool Visit(Progress node, LayoutNode laidOut) =>
        Announce(new(SemanticRole.ProgressIndicator, laidOut.Path ?? "", laidOut.Bounds,
            // `node.Spoken`, not `node.Value?.Spoken`: an indeterminate bar has no value and may
            // still have WORDS, which is the half #243 calls the real loss. "Estimating time
            // remaining" is most useful exactly where there is no number to fall back on.
            node.Label, node.Spoken, false, Range: node.Value));

    /// <summary>
    /// A navigable region is a GROUP: the reader names it and then walks its rows. Consuming it —
    /// the only shape available before <see cref="SemanticRole.Group"/> (#187) — would have hidden
    /// every row inside, which is why this declined while the web honoured it.
    /// </summary>
    public bool Visit(Navigable node, LayoutNode laidOut) =>
        AnnounceGroup(new(SemanticRole.Group, laidOut.Path ?? "", laidOut.Bounds,
            node.Label, null, false));

    /// <summary>
    /// A live region is a GROUP THE PLATFORM WATCHES. The urgency rides on the node rather than the
    /// role because that is how every platform models it — Android sets
    /// <c>accessibilityLiveRegion</c> on a container, UIKit posts an announcement — and because the
    /// region is still an ordinary group when nothing has changed.
    ///
    /// <para>
    /// PHOTON FENCE — the announcement itself does not happen here yet, and the reason is
    /// structural rather than a missing line. Announcing means noticing that a subtree CHANGED, and
    /// <c>PhotonHost.Semantics()</c> is a snapshot of the current frame with nothing to compare it
    /// against: no previous tree is kept and no pass diffs one. Posting from this walk would fire on
    /// every frame, which is worse than silence — a reader that repeats itself sixty times a second
    /// is a reader the user turns off. So what lands here is the DATA the announcement needs
    /// (<see cref="SemanticNode.Live"/> on a named group, at its bounds), and the frame-to-frame
    /// comparison plus one post per platform is its own slice.
    /// </para>
    ///
    /// <para>
    /// The web needs none of that: the DOM diff already knows what changed, so <c>aria-live</c> on
    /// the host IS the mechanism. That asymmetry is why this row closes on one target and not both,
    /// and it is stated rather than left to be discovered from a silent VoiceOver.
    /// </para>
    /// </summary>
    public bool Visit(LiveRegion node, LayoutNode laidOut) =>
        AnnounceGroup(new(SemanticRole.Group, laidOut.Path ?? "", laidOut.Bounds,
            node.Label, null, false, Live: node.Urgency));

    /// <inheritdoc cref="Wraps"/>
    public bool Visit(DragDismiss node, LayoutNode laidOut) => Wraps;

    /// <inheritdoc cref="Wraps"/>
    public bool Visit(Draggable node, LayoutNode laidOut) => Wraps;

    /// <inheritdoc cref="Wraps"/>
    public bool Visit(Hoverable node, LayoutNode laidOut) => Wraps;

    /// <inheritdoc cref="Wraps"/>
    public bool Visit(InFlow node, LayoutNode laidOut) => Wraps;

    /// <inheritdoc cref="Wraps"/>
    public bool Visit(InView node, LayoutNode laidOut) => Wraps;

    /// <inheritdoc cref="Decorative"/>
    public bool Visit(LoopMotion node, LayoutNode laidOut) => Decorative;

    /// <inheritdoc cref="Wraps"/>
    public bool Visit(Presence node, LayoutNode laidOut) => Wraps;

    /// <inheritdoc cref="Wraps"/>
    public bool Visit(Shortcut node, LayoutNode laidOut) => Wraps;

    /// <inheritdoc cref="Wraps"/>
    public bool Visit(Simulated node, LayoutNode laidOut) => Wraps;

    /// <summary>Every Text string under a node, joined — the derived accessible name of a control
    /// whose author gave it no explicit Label.</summary>
    private static string TextWithin(LayoutNode node)
    {
        var parts = new List<string>();
        Gather(node, parts);
        return string.Join(" ", parts);

        static void Gather(LayoutNode node, List<string> parts)
        {
            // PlainContent, for the reason the Text arm gives: a paragraph with runs has an empty
            // Content. Here it costs more than a missing announcement — a control whose label happens
            // to emphasise one word derived NO name at all, and a nameless button is announced as
            // "button" and nothing else.
            if (node.Source is Text { PlainContent.Length: > 0 } text) parts.Add(text.PlainContent);
            foreach (var child in node) Gather(child, parts);
        }
    }
}
