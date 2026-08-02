namespace eQuantic.UI.Primitives;

/// <summary>Composition every layer reaches for, expressed on the node itself.</summary>
public static class VisualNodeExtensions
{
    /// <summary>
    /// Puts this node in the MIDDLE of whatever contains it — the answer to "a fixed-size Box with a
    /// smaller child", which is an avatar's initials, a badge's number, a checkbox's tick, a radio's
    /// dot, an icon button's glyph.
    /// <para>
    /// A <see cref="Box"/> deliberately has no alignment: the child owns its own placement. And
    /// alignment needs SLACK — <c>MainAlign.Center</c> on a hugging row centres nothing, because a
    /// hugging row is exactly as wide as its content. So the wrapper fills both axes and then
    /// centres, which is the whole rule, in one place, instead of four lines repeated at every call
    /// site where it is easy to write two thirds of it and see a glyph sitting in a corner.
    /// </para>
    /// </summary>
    public static VisualNode Centered(this VisualNode node)
    {
        var row = new Row(gap: 0)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Main = MainAlign.Center,
            Cross = CrossAlign.Center,
        };
        row.Add(node);
        return row;
    }
}
