namespace eQuantic.UI.Primitives;

/// <summary>
/// A <see cref="SingleChildLayoutDelegate"/> laid out as a <see cref="MultiChildLayoutDelegate"/> of one child, so
/// a realizer has one door for both nodes: Flutter's three steps, in Flutter's order.
/// </summary>
internal sealed class SingleChildLayout(SingleChildLayoutDelegate single) : MultiChildLayoutDelegate
{
    /// <summary>The id the one child carries.</summary>
    public static readonly object ChildId = "child";

    public SingleChildLayoutDelegate Single { get; } = single;

    private protected override string Name => Single.GetType().Name;

    public override Size Layout(BoxConstraints constraints)
    {
        var size = constraints.Constrain(Single.GetSize(constraints));
        var child = LayoutChild(ChildId, Single.GetConstraintsForChild(constraints));
        PositionChild(ChildId, Single.GetPositionForChild(size, child));
        return size;
    }
}
