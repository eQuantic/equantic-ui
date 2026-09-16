using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>Text and the editable surfaces — four words, and all four announce.</summary>
internal sealed partial class SemanticsVisitor
{
    /// <summary>
    /// PLAIN content, never Content: a paragraph with runs carries an EMPTY Content — the words live
    /// in the spans — so reading the field dropped the whole node and a styled paragraph reached a
    /// screen reader as nothing at all. PlainContent's own summary says it is what accessibility
    /// reads; this is the caller that was not doing it.
    /// <para>Text with nothing to say keeps walking rather than announcing an empty stop.</para>
    /// </summary>
    public bool Visit(Text node, LayoutNode laidOut) =>
        node.PlainContent.Length > 0
            ? Announce(new(SemanticRole.StaticText, laidOut.Path ?? "", laidOut.Bounds,
                node.PlainContent, null, false, HeadingLevel: node.HeadingLevel))
            : PureLayout;

    /// <summary>
    /// The explicit Label names the field; the placeholder is only the fallback name (visually it
    /// vanishes under text). The VALUE is what the field holds.
    /// </summary>
    public bool Visit(TextEntry node, LayoutNode laidOut) =>
        Announce(new(SemanticRole.TextField, laidOut.Path ?? "", laidOut.Bounds,
            node.Label ?? node.Placeholder ?? "", node.Value, node.Disabled));

    /// <summary>A multiline editable code surface — a text area to a screen reader.</summary>
    public bool Visit(CodeSurface node, LayoutNode laidOut) =>
        Announce(new(SemanticRole.CodeField, laidOut.Path ?? "", laidOut.Bounds,
            node.Label ?? "", null, false));

    /// <summary>
    /// v1: a grid announces as an editable region with its label; per-cell semantics (the real AX
    /// grid role) joins with the component slice.
    /// </summary>
    public bool Visit(SheetSurface node, LayoutNode laidOut) =>
        Announce(new(SemanticRole.CodeField, laidOut.Path ?? "", laidOut.Bounds,
            node.Label ?? "", null, false));
}
