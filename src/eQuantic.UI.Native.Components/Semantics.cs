using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>What a node IS to assistive tech — the platform bridges map these onto their own
/// vocabularies (NSAccessibility roles, UIAccessibilityTraits, AccessibilityNodeInfo classes).</summary>
public enum SemanticRole : byte
{
    StaticText,
    Button,
    Link,
    TextField,
    /// <summary>A multiline editable code surface — a text area to a screen reader.</summary>
    CodeField,
    /// <summary>An Adjustable: one stop, arrows adjust (slider, segmented control, radio group).</summary>
    Slider,
    Image,
}

/// <summary>
/// One element a screen reader can land on, in READING ORDER (tree order — the same order Tab
/// walks). Identified by PATH, like every press, focus and scroll target: the tree is rebuilt every
/// frame, so a reference is stale by the time assistive tech acts on it, and the path is what stays.
/// </summary>
public readonly record struct SemanticNode(
    SemanticRole Role,
    string Path,
    Rect Bounds,
    string Label,
    string? Value,
    bool Disabled);

/// <summary>
/// Derives the SEMANTICS TREE from a realized frame: the page layout plus every overlay layout
/// (dialogs are exactly what a screen reader must not miss), walked in paint order. A pure
/// function of the frame — the bridges query it whenever assistive tech asks, and the
/// <c>SemanticsMatchFocusStops</c> parity test keeps this walk and the input walk honest against
/// each other.
/// <para>
/// Like <see cref="FocusStop"/> — and deliberately unlike pointer hit regions — content scrolled
/// out of the viewport is INCLUDED: linear navigation reaches the row below the fold and the view
/// scrolls to it; clipping exists so nobody can click what they cannot see, and a screen reader is
/// the opposite of a pointer.
/// </para>
/// </summary>
public static class SemanticsTree
{
    public static IReadOnlyList<SemanticNode> Collect(RealizeResult frame)
    {
        var nodes = new List<SemanticNode>();
        Walk(frame.Root, nodes);
        foreach (var overlay in frame.OverlayRoots)
            Walk(overlay, nodes);
        return nodes;
    }

    private static void Walk(LayoutNode node, List<SemanticNode> nodes)
    {
        switch (node.Source)
        {
            // A control's inner text IS its name, not a separate stop — the subtree is consumed,
            // exactly as the web's <button>text</button> reads as one element.
            case Pressable pressable:
                nodes.Add(new(SemanticRole.Button, node.Path ?? "", node.Bounds,
                    pressable.Label ?? TextWithin(node), null, pressable.Disabled));
                return;

            case Link link:
                nodes.Add(new(SemanticRole.Link, node.Path ?? "", node.Bounds,
                    link.Label ?? TextWithin(node), null, false));
                return;

            case TextEntry entry:
                // v1 name: the placeholder (never a label substitute visually, but the only name
                // the node carries today); the VALUE is what the field holds.
                nodes.Add(new(SemanticRole.TextField, node.Path ?? "", node.Bounds,
                    entry.Placeholder ?? "", entry.Value, entry.Disabled));
                return;

            // v1: a grid announces as an editable region with its label; per-cell semantics (the
            // real AX grid role) joins with the component slice.
            case SheetSurface sheetSurface:
                nodes.Add(new(SemanticRole.CodeField, node.Path ?? "", node.Bounds,
                    sheetSurface.Label ?? "", null, false));
                return;

            case CodeSurface code:
                nodes.Add(new(SemanticRole.CodeField, node.Path ?? "", node.Bounds,
                    code.Label ?? "", null, false));
                return;

            // One stop for the whole control, inner pressables stay pointer-only — the same rule
            // the focus route applies (InputSink.WithoutFocusStops).
            case Adjustable adjustable:
                nodes.Add(new(SemanticRole.Slider, node.Path ?? "", node.Bounds,
                    adjustable.Label, null, false));
                return;

            case Text text when text.Content.Length > 0:
                nodes.Add(new(SemanticRole.StaticText, node.Path ?? "", node.Bounds,
                    text.Content, null, false));
                return;

            // A labelled icon announces; an unlabelled one is decoration and stays silent.
            case Icon { Label: { Length: > 0 } label }:
                nodes.Add(new(SemanticRole.Image, node.Path ?? "", node.Bounds,
                    label, null, false));
                return;
        }

        foreach (var child in node.Children)
            Walk(child, nodes);
    }

    /// <summary>Every Text string under a node, joined — the derived accessible name of a control
    /// whose author gave it no explicit Label.</summary>
    private static string TextWithin(LayoutNode node)
    {
        var parts = new List<string>();
        Gather(node, parts);
        return string.Join(" ", parts);

        static void Gather(LayoutNode node, List<string> parts)
        {
            if (node.Source is Text { Content.Length: > 0 } text) parts.Add(text.Content);
            foreach (var child in node.Children) Gather(child, parts);
        }
    }
}
