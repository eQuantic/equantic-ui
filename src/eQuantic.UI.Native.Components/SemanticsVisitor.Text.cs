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
    /// vanishes under text). The VALUE is what the field holds, and an obscured field holds dots here
    /// as it does on screen: the text never reaches the tree, so no bridge can hand it to a reader.
    /// One dot per character as the eye counts them, what the platform's own secure field exposes.
    /// <para>
    /// A field a listbox hangs from is an EDITABLE COMBOBOX, and says whether its list shows, as the web
    /// makes its text control the combobox (#1083). Until #1088 it read as a plain text field here, and
    /// the option its arrows reached said nothing: the realizer now marks that row live while it is the
    /// one (<c>ItemRow</c>).
    /// </para>
    /// </summary>
    public bool Visit(TextEntry node, LayoutNode laidOut)
    {
        var listbox = ListboxHungFrom(laidOut);
        return Announce(new(listbox is null ? SemanticRole.TextField : SemanticRole.EditableComboBox,
            IdOf(laidOut), laidOut.Bounds,
            node.Label ?? node.Placeholder ?? "",
            node.Obscure ? new string('•', new System.Globalization.StringInfo(node.Value).LengthInTextElements) : node.Value,
            node.Disabled, Expanded: listbox?.Open, Obscured: node.Obscure));
    }

    /// <summary>
    /// The <see cref="Anchored"/> whose listbox this field's keys walk, or null: the nearest anchored node
    /// with a panel role above the field, when its panel is a listbox and the field is the first one its
    /// anchor holds, wherever it sits there. That is the element the web makes the combobox
    /// (<c>TextControlIn</c>, the anchor's first text control), so a field beside an icon in the anchor is
    /// one, unlike a pressable, which is the combobox only when it IS the anchor
    /// (<see cref="AnchorsAListbox"/>). The panel never lays out under the anchor here: it is an overlay
    /// root of its own.
    /// </summary>
    private static Anchored? ListboxHungFrom(LayoutNode laidOut)
    {
        for (var node = laidOut.Parent; node is not null; node = node.Parent)
        {
            if (node.Source is not Anchored { PanelRole: not AnchorPanelRole.None } anchored) continue;
            return anchored.PanelRole == AnchorPanelRole.Listbox && ReferenceEquals(FirstField(node), laidOut)
                ? anchored
                : null;
        }
        return null;
    }

    /// <summary>The first text field under <paramref name="node"/>, in tree order, or null.</summary>
    private static LayoutNode? FirstField(LayoutNode node)
    {
        if (node.Source is TextEntry) return node;
        foreach (var child in node)
            if (FirstField(child) is { } field) return field;
        return null;
    }

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
