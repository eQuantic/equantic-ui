using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Interaction and motion — thirteen words. Three are controls and announce as one stop each; nine
/// wrap a child or are ornament; the thirteenth, <see cref="Navigable"/>, is the gap.
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
            node.Role == AdjustableRole.Slider ? node.Value?.Spoken : null,
            false));

    /// <inheritdoc cref="AwaitsGroupRole"/>
    public bool Visit(Navigable node, LayoutNode laidOut) => AwaitsGroupRole;

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
