using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// A radial menu, written in C# (#1556): every child centred on a circle of <c>radius</c> around the middle of the
/// node, the first at the top and the rest clockwise.
/// </summary>
public sealed class RadialLayout(float radius) : MultiChildLayoutDelegate
{
    public override void PerformLayout(Size size)
    {
        var count = 0;
        while (HasChild(count)) count++;
        var centre = new Point(size.Width / 2, size.Height / 2);
        for (var i = 0; i < count; i++)
        {
            var child = LayoutChild(i, BoxConstraints.Loose(size));
            var angle = 2 * MathF.PI * i / count - MathF.PI / 2;
            PositionChild(i, new Point(centre.X + radius * MathF.Cos(angle) - child.Width / 2,
                centre.Y + radius * MathF.Sin(angle) - child.Height / 2));
        }
    }
}
