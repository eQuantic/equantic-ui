namespace eQuantic.UI.Primitives;

/// <summary>
/// The children a layout delegate lays out, as a realizer binds them for one layout: Photon in its layout
/// pass, the browser right after its own. A delegate reaches them only through its <c>LayoutChild</c>,
/// <c>PositionChild</c> and <c>HasChild</c>, which check what every realizer would otherwise check alike.
/// </summary>
/// <remarks>
/// The browser binds the children with the runtime's own <c>LayoutChildren</c>, in <c>layout-delegates.ts</c>.
/// </remarks>
public interface ILayoutChildren
{
    /// <summary>Whether a child carries <paramref name="id"/>.</summary>
    bool Has(object id);

    /// <summary>Lays the child that carries <paramref name="id"/> out as the content of a box with the
    /// <paramref name="constraints"/> as its bounds, and answers its size.</summary>
    Size Layout(object id, BoxConstraints constraints);

    /// <summary>Places the child that carries <paramref name="id"/> at <paramref name="offset"/> from the
    /// node's origin.</summary>
    void Position(object id, Point offset);
}
