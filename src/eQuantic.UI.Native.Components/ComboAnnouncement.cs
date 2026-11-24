using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// What an editable combobox's list says in a frame (#1088): the option the keyboard is on, said from the
/// FIELD, which keeps the keyboard, as the web's reader follows the field's <c>aria-activedescendant</c>.
/// <para>
/// The option is the one <see cref="Anchored.ActiveIndex"/> counts to over the panel's item rows in tree order,
/// the rows the web numbers for that attribute: never the one marked selected, which in a list of choices is the
/// one picked, not the one the arrows are on. It is read from the layer's layout, so a row the paint walk clips
/// still counts.
/// </para>
/// <para>
/// The list says ONE thing a frame, and the empty text when no row is active or the list is closed: a reader
/// then hears the option again when the keyboard comes back to it, as it does when the web sets the field's
/// active descendant again. What it said is kept on the ANCHORED node's position (<paramref name="keeps"/>),
/// which keeps no state of its own: the field's keeps its text, and a position keeps one kind of state. It
/// leaves the tree with the combobox.
/// </para>
/// </summary>
/// <param name="keeps">The anchored node's position, which keeps what the list last said.</param>
/// <param name="field">The field's position, whose semantic node the option is said from.</param>
/// <param name="anchored">This frame's anchored node, the subtree of a mark that says nothing.</param>
/// <param name="active">The anchored node's <see cref="Anchored.ActiveIndex"/>: -1 for no row.</param>
internal sealed class ComboAnnouncement(Element keeps, Element field, LayoutNode anchored, int active)
{
    /// <summary>
    /// The text field of the anchored node <paramref name="anchored"/>: the first <see cref="TextEntry"/> of its
    /// anchor in tree order, wherever it sits, as the web keys the first text control the anchor holds
    /// (<c>TextControlIn</c>). Null when the anchor holds none. The semantics walk and the emit walk both ask
    /// this, so the field a reader hears as the combobox is the one its list is said from.
    /// </summary>
    public static LayoutNode? FieldOf(LayoutNode anchored)
    {
        foreach (var child in anchored.Children)
        {
            if (child.Source is TextEntry) return child;
            if (FieldOf(child) is { } field) return field;
        }
        return null;
    }

    /// <summary>The mark of a combobox whose list is not shown: it says nothing, so the option the list
    /// shows when it opens again is news.</summary>
    public static LiveRegionMark Silent(Element keeps, Element field, LayoutNode anchored) =>
        new(keeps, "", LiveRegionUrgency.Polite, anchored, "", field);

    /// <summary>The list's mark for this frame: the row of <paramref name="layer"/> the keyboard is on, or
    /// nothing when no row is active.</summary>
    public LiveRegionMark Mark(LayoutNode layer)
    {
        var seen = 0;
        return active >= 0 && Row(layer, ref seen) is { } row
            ? new(keeps, "", LiveRegionUrgency.Polite, row, ((Pressable)row.Source).Label, field)
            : Silent(keeps, field, anchored);
    }

    /// <summary>The item row numbered <c>active</c> under <paramref name="node"/>, counting from
    /// <paramref name="seen"/> in tree order, as the web numbers them.</summary>
    private LayoutNode? Row(LayoutNode node, ref int seen)
    {
        if (node.Source is Pressable { Role: PressableRole.Option or PressableRole.MenuItem })
        {
            if (seen == active) return node;
            seen++;
        }
        foreach (var child in node.Children)
            if (Row(child, ref seen) is { } row) return row;
        return null;
    }
}
