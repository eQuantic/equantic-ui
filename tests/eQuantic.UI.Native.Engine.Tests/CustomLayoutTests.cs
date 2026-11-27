using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A <see cref="CustomMultiChildLayout"/> on Photon (#1556): the app's delegate runs in the layout pass, as
/// Flutter's does, told the room a filling box has, and each child is measured with the constraints it chooses
/// and placed where it says. Driven through a real <see cref="PhotonHost"/>, so the children are the ones the
/// element tree built and the places are the ones the frame draws.
/// </summary>
public class CustomLayoutTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static LayoutNode Laid(VisualNode root, float width = 400, float height = 600)
    {
        var host = new PhotonHost(root, Theme, ThemeMode.Light, width, height);
        return host.RenderFrame(new DisplayListBuilder()).Root;
    }

    private static LayoutNode Find(LayoutNode node, Func<VisualNode, bool> match)
    {
        if (match(node.Source)) return node;
        foreach (var child in node.Children)
            if (Find(child, match) is { } found) return found;
        return null!;
    }

    private static LayoutNode LayoutOf(LayoutNode root) =>
        Find(root, node => node is CustomMultiChildLayout or CustomSingleChildLayout);

    private static Box Square(float side) => new(new BoxStyle { Width = side, Height = side });

    /// <summary>Six children on a circle around the centre, the radial menu of the issue.</summary>
    private sealed class Radial(int count, float radius) : MultiChildLayoutDelegate
    {
        public override void PerformLayout(Size size)
        {
            var centre = new Point(size.Width / 2, size.Height / 2);
            for (var i = 0; i < count; i++)
            {
                var child = LayoutChild(i, BoxConstraints.Loose(new Size(48, 48)));
                var angle = 2 * MathF.PI * i / count;
                PositionChild(i, new Point(centre.X + radius * MathF.Cos(angle) - child.Width / 2,
                    centre.Y + radius * MathF.Sin(angle) - child.Height / 2));
            }
        }
    }

    /// <summary>Two columns of half the room's width, each child under the shorter column: sized by its children.</summary>
    private sealed class Masonry(int count) : MultiChildLayoutDelegate
    {
        public override Size Layout(BoxConstraints constraints)
        {
            var column = constraints.MaxWidth / 2;
            var heights = new float[2];
            for (var i = 0; i < count; i++)
            {
                var into = heights[0] <= heights[1] ? 0 : 1;
                var child = LayoutChild(i, new BoxConstraints(column, column, 0, float.PositiveInfinity));
                PositionChild(i, new Point(into * column, heights[into]));
                heights[into] += child.Height;
            }
            return new Size(constraints.MaxWidth, MathF.Max(heights[0], heights[1]));
        }
    }

    private sealed class Telling(List<BoxConstraints> told) : MultiChildLayoutDelegate
    {
        public override Size Layout(BoxConstraints constraints)
        {
            told.Add(constraints);
            LayoutChild("only", constraints.Loosen());
            return new Size(10, 10);
        }
    }

    [Fact]
    public void ARadialMenu_PlacesEachChildOnItsCircle()
    {
        var layout = new CustomMultiChildLayout(new Radial(6, 96),
            [.. Enumerable.Range(0, 6).Select(i => new LayoutId(i, Square(48)))]);
        var laid = LayoutOf(Laid(new Box(new BoxStyle { Width = 240, Height = 240 }, layout)));

        laid.Bounds.Width.Should().Be(240);
        laid.Bounds.Height.Should().Be(240);
        for (var i = 0; i < 6; i++)
        {
            var angle = 2 * MathF.PI * i / 6;
            var child = laid.Children[i].Bounds;
            child.X.Should().BeApproximately(120 + 96 * MathF.Cos(angle) - 24, 0.01f, $"child {i} stands on the circle");
            child.Y.Should().BeApproximately(120 + 96 * MathF.Sin(angle) - 24, 0.01f, $"child {i} stands on the circle");
            child.Width.Should().Be(48);
        }
    }

    [Fact]
    public void AMasonryGrid_IsSizedByItsChildren_EachUnderTheShorterColumn()
    {
        float[] heights = [100, 50, 70, 30];
        var layout = new CustomMultiChildLayout(new Masonry(heights.Length),
            [.. heights.Select((h, i) => new LayoutId(i, new Box(new BoxStyle { Height = h })))]);
        var laid = LayoutOf(Laid(new Box(new BoxStyle { Width = 320 }, layout)));

        laid.Bounds.Width.Should().Be(320, "the width is the room's, which the parent decides");
        laid.Bounds.Height.Should().Be(130, "the height is the taller column's, which the delegate answered");
        var places = laid.Children.Select(child => (child.Bounds.X, child.Bounds.Y, child.Bounds.Width)).ToList();
        places.Should().Equal((0f, 0f, 160f), (160f, 0f, 160f), (160f, 50f, 160f), (0f, 100f, 160f));
    }

    [Fact]
    public void TheDelegate_IsToldTheRoomAFillingBoxHas()
    {
        var told = new List<BoxConstraints>();
        Laid(new Box(new BoxStyle { Width = 320 }, new CustomMultiChildLayout(new Telling(told),
            [new LayoutId("only", Square(10))])));

        told[^1].Should().Be(new BoxConstraints(0, 320, 0, float.PositiveInfinity),
            "the width the parent decides, and an unbounded height the box's content sizes");
    }

    [Fact]
    public void ASingleChild_IsCentredByItsDelegate()
    {
        var layout = new CustomSingleChildLayout(new Centred(), new Box(new BoxStyle { Width = 50, Height = 20 }));
        var laid = LayoutOf(Laid(new Box(new BoxStyle { Width = 200, Height = 100 }, layout)));

        laid.Bounds.Should().Be(new Rect(0, 0, 200, 100));
        laid.Children.Single().Bounds.Should().Be(new Rect(75, 40, 50, 20));
    }

    private sealed class Centred : SingleChildLayoutDelegate
    {
        public override Point GetPositionForChild(Size size, Size childSize) =>
            new((size.Width - childSize.Width) / 2, (size.Height - childSize.Height) / 2);
    }

    /// <summary>A delegate places, it does not reorder: a reader reaches the children in their order.</summary>
    [Fact]
    public void TheChildren_AreReadInTheirOrder_WhereverTheyArePlaced()
    {
        var layout = new CustomMultiChildLayout(new Reversed(3),
            [.. new[] { "first", "second", "third" }.Select((word, i) => new LayoutId(i, new Text(word, TypeRole.BodyM)))]);
        var host = new PhotonHost(new Box(new BoxStyle { Width = 300, Height = 300 }, layout), Theme, ThemeMode.Light, 400, 400);
        host.RenderFrame(new DisplayListBuilder());

        host.Semantics().Where(s => s.Role == SemanticRole.StaticText).Select(s => s.Label)
            .Should().Equal("first", "second", "third");
    }

    private sealed class Reversed(int count) : MultiChildLayoutDelegate
    {
        public override void PerformLayout(Size size)
        {
            for (var i = 0; i < count; i++)
            {
                LayoutChild(i, BoxConstraints.Loose(size));
                PositionChild(i, new Point(0, (count - 1 - i) * 40));
            }
        }
    }

    [Fact]
    public void AnOptionalChild_IsLaidOutOnlyWhenItIsThere()
    {
        var layout = new CustomMultiChildLayout(new WithBadge(), [new LayoutId("body", Square(40))]);
        var laid = LayoutOf(Laid(new Box(new BoxStyle { Width = 100, Height = 100 }, layout)));

        laid.Children.Single().Bounds.Should().Be(new Rect(0, 0, 40, 40));
    }

    private sealed class WithBadge : MultiChildLayoutDelegate
    {
        public override void PerformLayout(Size size)
        {
            LayoutChild("body", BoxConstraints.Loose(size));
            PositionChild("body", Point.Zero);
            if (!HasChild("badge")) return;
            LayoutChild("badge", BoxConstraints.Loose(size));
            PositionChild("badge", new Point(size.Width - 10, 0));
        }
    }

    public static TheoryData<string, MultiChildLayoutDelegate, string> Refusals => new()
    {
        { "twice", new Twice(), "twice" },
        { "an unknown id", new Unknown(), "which no child carries" },
        { "a child left out", new LeftOut(), "did not lay out the child with the id '1'" },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public void ADelegateThatBreaksTheRule_IsRefused_NamingItself(string _, MultiChildLayoutDelegate layoutDelegate, string says)
    {
        var layout = new CustomMultiChildLayout(layoutDelegate, [new LayoutId(0, Square(10)), new LayoutId(1, Square(10))]);
        var laying = () => Laid(new Box(new BoxStyle { Width = 100, Height = 100 }, layout));

        laying.Should().Throw<InvalidOperationException>().WithMessage($"*{layoutDelegate.GetType().Name}*{says}*");
    }

    private sealed class Twice : MultiChildLayoutDelegate
    {
        public override void PerformLayout(Size size)
        {
            LayoutChild(0, BoxConstraints.Loose(size));
            LayoutChild(0, BoxConstraints.Loose(size));
        }
    }

    private sealed class Unknown : MultiChildLayoutDelegate
    {
        public override void PerformLayout(Size size) => LayoutChild(7, BoxConstraints.Loose(size));
    }

    private sealed class LeftOut : MultiChildLayoutDelegate
    {
        public override void PerformLayout(Size size) => LayoutChild(0, BoxConstraints.Loose(size));
    }

    /// <summary>The default size is the room's biggest, which an axis sized by content leaves infinite.</summary>
    [Fact]
    public void AnInfiniteSizeOnAnUnboundedAxis_IsRefused()
    {
        var layout = new CustomMultiChildLayout(new LeftOut(), [new LayoutId(0, Square(10))]);
        var laying = () => Laid(new Box(new BoxStyle { Width = 100 }, layout));

        laying.Should().Throw<InvalidOperationException>().WithMessage("*LeftOut*size is finite*");
    }

    [Fact]
    public void TwoChildrenWithOneId_AreRefused()
    {
        var building = () => new CustomMultiChildLayout(new LeftOut(), [new LayoutId(0, Square(10)), new LayoutId(0, Square(10))]);

        building.Should().Throw<ArgumentException>().WithMessage("*carry the id '0'*");
    }
}
