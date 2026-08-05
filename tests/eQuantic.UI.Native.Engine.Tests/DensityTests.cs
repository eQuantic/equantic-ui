using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// DENSITY is the target's, never the caller's: the same tree measures one way under a thumb and
/// another under a pointer. What this pins is that the components ASK — three of them used to keep
/// their own copy of the ladder in a private table (Avatar, IconButton, Switch), and a copy cannot
/// follow anything.
/// </summary>
public class DensityTests
{
    private static LayoutNode Find(LayoutNode node, Func<LayoutNode, bool> match)
    {
        if (match(node)) return node;
        foreach (var child in node.Children)
        {
            var found = Find(child, match);
            if (found is not null) return found;
        }
        return null!;
    }

    private static LayoutNode Realize(VisualNode root, Density density)
    {
        var builder = new DisplayListBuilder();
        return PhotonRealizer.Realize(root, 400, 200, PhotonTheme.Instance, ThemeMode.Light, builder,
            density: density).Root;
    }

    [Theory]
    [InlineData(Density.Comfortable, 52, 32)]
    [InlineData(Density.Compact, 44, 26)]
    public void TheSwitchTakesTheTargetsLadder(Density density, float width, float height)
    {
        var root = Realize(new Switch(true, () => { }), density);

        var track = Find(root, n => n.Source is Box { Style.Background: not null }
            && MathF.Abs(n.Bounds.Width - width) < 0.5f);
        track.Should().NotBeNull($"the track is {width}dp wide at {density}");
        track.Bounds.Height.Should().BeApproximately(height, 0.5f);
    }

    [Theory]
    [InlineData(Density.Comfortable, 22)]
    [InlineData(Density.Compact, 20)]
    public void TheCheckboxDoesToo(Density density, float side)
    {
        var root = Realize(new Checkbox(true, () => { }, "Terms"), density);

        var box = Find(root, n => n.Source is Box { Style.CornerRadius.TopLeft: > 0 }
            && MathF.Abs(n.Bounds.Width - n.Bounds.Height) < 0.5f
            && n.Bounds.Width > 10);
        box.Bounds.Width.Should().BeApproximately(side, 0.5f);
    }

    [Fact]
    public void AControlsHitTargetStopsInflatingWhenThePointerIsPrecise()
    {
        // Under a finger the §08 minimum expands the press rect past the control; under a pointer
        // it must not — on a toolbar of 26dp buttons those invisible margins would overlap.
        var builder = new DisplayListBuilder();
        var comfortable = PhotonRealizer.Realize(new Button("Go", size: SizeVariant.Small), 400, 200,
            PhotonTheme.Instance, ThemeMode.Light, builder);
        var compact = PhotonRealizer.Realize(new Button("Go", size: SizeVariant.Small), 400, 200,
            PhotonTheme.Instance, ThemeMode.Light, new DisplayListBuilder(), density: Density.Compact);

        comfortable.HitRegions[0].Bounds.Height.Should().BeApproximately(Touch.MinTarget, 0.5f);
        compact.HitRegions[0].Bounds.Height.Should().BeApproximately(
            Sizing.Height(SizeVariant.Small, Density.Compact), 0.5f);
    }
}
