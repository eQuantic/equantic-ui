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
    public bool Visit(Text node, LayoutNode laidOut)
    {
        var announced = node.PlainContent.Length > 0
            ? Announce(new(SemanticRole.StaticText, IdOf(laidOut), laidOut.Bounds,
                node.PlainContent, null, false, HeadingLevel: node.HeadingLevel))
            : PureLayout;

        // A LINKED RUN announces after the paragraph that holds it, which is the order a reader
        // walks: the sentence, then the things inside it you can follow. Announced at the identity
        // the input route gives it, so the bridge's activate arrives at the same link the keyboard
        // stops on — a stop nothing names is the empty offer #256 spent two rounds removing.
        foreach (var link in RichTextRuns.LinksOf(laidOut, node))
            _nodes.Add(new SemanticNode(SemanticRole.Link, link.Id, link.Bounds, link.Text, null, false));

        return announced;
    }

    /// <summary>
    /// The explicit Label names the field; the placeholder is only the fallback name (visually it
    /// vanishes under text). The VALUE is what the field holds.
    /// </summary>
    public bool Visit(TextEntry node, LayoutNode laidOut) =>
        Announce(new(SemanticRole.TextField, IdOf(laidOut), laidOut.Bounds,
            node.Label ?? node.Placeholder ?? "", node.Value, node.Disabled));

    /// <summary>A multiline editable code surface — a text area to a screen reader.</summary>
    public bool Visit(CodeSurface node, LayoutNode laidOut)
    {
        Announce(new(SemanticRole.CodeField, IdOf(laidOut), laidOut.Bounds,
            node.Label ?? "", null, false));
        // What the surface OFFERS at its caret is no part of the code's text: after the field it
        // belongs to, its rows announce as options, the one the keyboard is on selected.
        for (var i = 1; i < laidOut.Children.Count; i++) SemanticsTree.Walk(laidOut.Children[i], this);
        return Consumed;
    }

    /// <summary>
    /// A spreadsheet's surface announces as the editable field that takes its keys, with its label, and what
    /// it draws inside after it: the grid its table is, with its headers and its cells (#1081). The field's
    /// value is what the grid says now (<see cref="SheetSurface.Announcement"/>), so a reader arriving on it
    /// hears where it is, and the field is the polite live region a shell marks, so the moves are heard
    /// wherever the reader is (web twin: the surface's hidden status).
    /// </summary>
    public bool Visit(SheetSurface node, LayoutNode laidOut)
    {
        Announce(new(SemanticRole.CodeField, IdOf(laidOut), laidOut.Bounds,
            node.Label ?? "", node.Announcement.Length > 0 ? node.Announcement : null, false,
            Live: LiveRegionUrgency.Polite));
        foreach (var child in laidOut.Children) SemanticsTree.Walk(child, this);
        return Consumed;
    }
}
