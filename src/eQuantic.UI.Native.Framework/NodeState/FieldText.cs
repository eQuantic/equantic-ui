using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// What a field shows on Photon: what was typed into it, until the page builds it again, and then the
/// value the page hands it, whether or not that value changed since the last build (#996). It is
/// Flutter's <c>EditableText</c>, whose controller keeps the text and takes the app's value when its
/// parent configures it again, and it is what the web shows after a render (#1026).
/// <para>
/// The text lives on the field's position, so it leaves the tree with the field, and a field drawn at
/// another's place starts from its own value. Every reader asks <see cref="Of"/>: the measure, the
/// paint, the semantics, the caret and the selection, and the editing value a phone's keyboard reads.
/// A reader that took the node's value instead would measure one text and draw another.
/// </para>
/// <para>
/// The rule is NOT "take the page's value when it differs from the last one". A page that resets a
/// field to the value it held before the typing hands the same value twice, and that rule kept the typed
/// text over a field the page had emptied (eQuantic.Auth's sign-in, #1026).
/// </para>
/// </summary>
public static class FieldText
{
    /// <summary>
    /// The text the field at <paramref name="at"/> shows. The position's own node decides: one the field
    /// has not taken its text from means the page built the field again, so the field takes that node's
    /// value; the same node means it did not, and the field keeps what was typed. With no position (one
    /// that left the tree, or none at all) it is the value of <paramref name="entry"/>.
    /// <para>
    /// The position's node, never the caller's: whether the page built the field is a fact about the
    /// position. A reader holding the node of an older frame (the host keeps its last frame until the
    /// next is drawn) reads what the field shows, and cannot put the field back to that node's value.
    /// </para>
    /// </summary>
    public static string Of(Element? at, TextEntry entry)
    {
        if (NodeStates.For<FieldTextState>(at) is not { } state) return entry.Value;
        var built = BuiltWith(at!, entry);
        if (!ReferenceEquals(state.From, built))
        {
            state.From = built;
            state.Text = built.Value;
        }

        return state.Text;
    }

    /// <summary>
    /// Records that <paramref name="text"/> was typed into the field at <paramref name="at"/>, which the
    /// page built with <paramref name="entry"/>: the field shows it until the page builds it again. A
    /// position that left the tree keeps nothing.
    /// </summary>
    public static void Typed(Element? at, TextEntry entry, string text)
    {
        if (NodeStates.For<FieldTextState>(at) is not { } state) return;
        state.From = BuiltWith(at!, entry);
        state.Text = text;
    }

    /// <summary>The node the page last built the field at <paramref name="at"/> with: the position's own,
    /// or <paramref name="entry"/> for a position that holds no field.</summary>
    private static TextEntry BuiltWith(Element at, TextEntry entry) => at.Configuration as TextEntry ?? entry;
}
