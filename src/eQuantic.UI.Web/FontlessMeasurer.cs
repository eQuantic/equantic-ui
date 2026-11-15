using System.Runtime.CompilerServices;

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
    /// <summary>A measurer for a tree whose components build through <paramref name="boundary"/>.</summary>
    public FontlessMeasurer(ComponentBoundary boundary) => Boundary = boundary;

    /// <summary>The boundary of the tree this measurer serves (#1452): what its components build
    /// through, and where what one of them failed is reported and counted.</summary>
    public ComponentBoundary Boundary { get; }

    private static readonly object Asked = new();

    /// <summary>The instances whose last build through <see cref="Build"/> asked for a width. Weak, so
    /// a host kept for many frames holds no component that left its tree.</summary>
    private readonly ConditionalWeakTable<UiComponent, object> _unmeasured = new();

    /// <summary>How many widths were asked for, each answered 0.</summary>
    public int Asks { get; private set; }

    public float Measure(string text, TypeStyle style)
    {
        Asks++;
        return 0;
    }

    /// <summary>
    /// How a component of a <see cref="MarkupHost"/>'s tree builds: through the boundary, as every
    /// owner's does, noting whether its build asked for a width. In a frame the owner builds every
    /// component before the realizer lowers any, so the count around a component's lowering cannot
    /// say which one asked, and this can.
    /// </summary>
    public VisualNode Build(UiComponent component, ComponentContext context, int depth)
    {
        var asks = Asks;
        var built = Boundary.BuildAtDepth(component, context, depth);
        if (Asks > asks) _unmeasured.AddOrUpdate(component, Asked);
        else _unmeasured.Remove(component);
        return built;
    }

    /// <summary>Whether <paramref name="component"/>'s last build through <see cref="Build"/> asked for
    /// a width: what it built stands on widths nobody measured.</summary>
    public bool BuiltUnmeasured(UiComponent component) => _unmeasured.TryGetValue(component, out _);
}
