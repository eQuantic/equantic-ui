using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The RealizeResult LIFETIME that unlocks node pooling — the exact ownership story whose absence
/// once rewrote trees under 155 retained results. Opt-in (`RecycleFrames`): a production shell
/// owns every frame it replaces and feeds it to the next; everything else keeps immutable trees.
/// </summary>
public class NodePoolTests
{
    private readonly ITestOutputHelper _output;
    public NodePoolTests(ITestOutputHelper output) => _output = output;

    private sealed class Page : Primitives.StatefulComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2) { Padding = EdgeInsets.All(Space.S3) };
            for (var i = 0; i < 12; i++)
            {
                var row = new Row(gap: Space.S2);
                row.Add(new Box(new BoxStyle { Width = 24, Height = 24, Background = context.Theme.SurfaceSubtle }));
                row.Add(new Text($"Row {i}", TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1));
                row.Add(new Pressable(new Text("Open", TypeRole.Label, context.Theme.TextPrimary), () => { }));
                column.Add(row);
            }
            return column;
        }
    }

    [Fact]
    public void RecyclingHost_FeedsTheReplacedFrame_IntoTheNext()
    {
        var host = new PhotonHost(new Page(), PhotonTheme.Instance, ThemeMode.Light, 400, 500)
        {
            RecycleFrames = true,
        };

        var first = host.RenderFrame(new DisplayListBuilder());
        var retained = new HashSet<LayoutNode>(ReferenceEqualityComparer.Instance);
        Collect(first.Root, retained);

        host.RenderFrame(new DisplayListBuilder(), 16);   // replaces FIRST → its tree parks in the pool
        var third = host.RenderFrame(new DisplayListBuilder(), 32);

        var reused = 0;
        CountReused(third.Root, retained, ref reused);
        reused.Should().BeGreaterThan(0,
            "the third frame is built from the first frame's recycled nodes");
        _output.WriteLine($"third frame reused {reused} of {retained.Count} first-frame nodes");
    }

    [Fact]
    public void DefaultHost_NeverTouchesARetainedFrame()
    {
        var host = new PhotonHost(new Page(), PhotonTheme.Instance, ThemeMode.Light, 400, 500);

        var retained = host.RenderFrame(new DisplayListBuilder());
        var rootChildren = retained.Root.Children.Count;
        var firstNodes = new HashSet<LayoutNode>(ReferenceEqualityComparer.Instance);
        Collect(retained.Root, firstNodes);

        RealizeResult last = retained;
        for (var frame = 1; frame <= 5; frame++)
            last = host.RenderFrame(new DisplayListBuilder(), frame * 16f);

        retained.Root.Children.Count.Should().Be(rootChildren, "a retained tree is immutable");
        var reused = 0;
        CountReused(last.Root, firstNodes, ref reused);
        reused.Should().Be(0, "without opt-in, no node is ever shared between frames");
    }

    private static void Collect(LayoutNode node, HashSet<LayoutNode> into)
    {
        into.Add(node);
        foreach (var child in node.Children) Collect(child, into);
    }

    private static void CountReused(LayoutNode node, HashSet<LayoutNode> pool, ref int reused)
    {
        if (pool.Contains(node)) reused++;
        foreach (var child in node.Children) CountReused(child, pool, ref reused);
    }
}
