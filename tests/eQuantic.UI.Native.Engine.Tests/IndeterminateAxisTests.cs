using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Fill means "take what the parent gives". On an axis the parent is sizing FROM ITS CONTENT there
/// is nothing to take: a child that grabs the maximum available would decide the size of the very
/// thing meant to measure it. That is what made a 16dp Badge come out as wide as the window and
/// push its neighbours off the card — and the goldens had been blessing it, because a stretched
/// badge is still a badge-coloured rectangle.
/// <para>
/// The rule is CSS shrink-to-fit's and Flutter's min-size row's: Fill on an indeterminate axis
/// resolves to the intrinsic size.
/// </para>
/// </summary>
public class IndeterminateAxisTests
{
    /// <summary>Builds a FRESH tree each time, the way a real component does — a node handed back
    /// twice is not what the host ever sees.</summary>
    private sealed class Page(Func<VisualNode> build) : Primitives.StatefulComponent
    {
        public override VisualNode Build(ComponentContext context) => build();
    }

    private static LayoutNode Measure(Func<VisualNode> root, float width = 600)
    {
        var host = new PhotonHost(new Page(root), PhotonTheme.Instance, ThemeMode.Light, width, 200);
        host.RenderFrame(new DisplayListBuilder());
        return host.RenderFrame(new DisplayListBuilder(), 1000).Root;
    }

    /// <summary>The first node of a given kind, wherever the host wrapped it.</summary>
    private static LayoutNode Find<T>(LayoutNode node) where T : VisualNode
    {
        if (node.Source is T) return node;
        foreach (var child in node.Children)
            if (Find<T>(child) is { } found) return found;
        return null!;
    }

    [Fact]
    public void ABadge_IsBadgeSized_NotWindowSized()
    {
        var tree = Measure(() =>
        {
            var row = new Row(gap: 12) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
            row.Add(new Badge(3));
            row.Add(new Chip("Beta", ChipKind.Tag));
            return row;
        });

        var badge = Find<Badge>(tree);
        badge.Bounds.Width.Should().BeLessThan(40, "a count badge hugs its digits");
    }

    [Fact]
    public void ACenteredChild_DoesNotDragItsHuggingParent()
    {
        // Centred content is the shape that exposed this: centring NEEDS Fill (a hugging row has no
        // slack to centre within), so the wrapper legitimately asks for everything.
        var tree2 = Measure(() =>
        {
            var row = new Row(gap: 0) { Width = SizeValue.Fill };
            row.Add(new Box(new BoxStyle { MinWidth = 16, Padding = EdgeInsets.Symmetric(4, 0) },
                new Text("9", TypeRole.Caption).Centered()));
            return row;
        });
        Find<Box>(tree2).Bounds.Width.Should().BeLessThan(60);
    }

    [Fact]
    public void AFillChild_OfASIZEDParent_StillFills()
    {
        var box = Find<Box>(Measure(() => new Box(new BoxStyle { Width = 200, Height = 40 },
            new Text("x", TypeRole.Caption).Centered())));
        box.Bounds.Width.Should().Be(200);
        box.Children[0].Bounds.Width.Should().Be(200, "the parent HAS a size to fill");
    }

    [Fact]
    public void AFillChild_OfAFillParent_StillFills()
    {
        Find<Box>(Measure(() => new Box(new BoxStyle { Width = SizeValue.Fill, Height = 40 },
            new Row(gap: 0) { Width = SizeValue.Fill, Height = 20 })))
            .Children[0].Bounds.Width.Should().Be(600);
    }
}
