using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// How wide text is, answered where there is no font to ask: 0, every time, and COUNTED.
/// <para>
/// The server lays a page out with no font on hand, so a component whose geometry IS text geometry
/// (a code block's gutter as wide as its widest number, a column one advance of the mono face) is
/// built on zeros here. That used to be invisible: hydration keeps the server's markup, so the
/// client adopted the zeros and kept them, and every code block the server sent had a 12px gutter
/// for as long as the page lived. Counting the questions is how the realizer learns WHICH
/// components were built on them, and it marks each one for the client to draw again (see
/// <c>WebLoweringVisitor.Visit(UiComponent)</c>).
/// </para>
/// </summary>
internal sealed class FontlessMeasurer
{
    /// <summary>How many widths were asked for, each answered 0.</summary>
    public int Asks { get; private set; }

    public float Measure(string text, TypeStyle style)
    {
        Asks++;
        return 0;
    }
}
