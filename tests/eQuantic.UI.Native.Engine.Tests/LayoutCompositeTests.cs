using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The composite, read both ways. A laid-out node knows the node that placed it — recursively, to
/// the root — and the link cannot be forgotten because there is no way to attach a child without
/// it.
///
/// <para>
/// This is the tree Flutter makes bidirectional too: its <c>RenderObject</c> carries a
/// <c>parent</c> while its <c>Widget</c> carries none. The asymmetry is deliberate on both sides —
/// a widget is rebuilt constantly, so a back-reference on it means nothing, while the laid-out tree
/// is walked top-down and the parent is already in hand. <see cref="VisualNode"/> therefore has no
/// Parent and should not grow one.
/// </para>
///
/// <para>
/// Before this, every "where am I" question was answered by a STRING: the layout path, keyed by
/// <c>(Parent, Index, Key)</c>, and read thirteen times in the semantics walk alone. Paths encode
/// ancestry precisely because there was no reference to follow — and a consumer building a splitter
/// ended up using a drawing primitive as a measuring tape for the same reason.
/// </para>
/// </summary>
public class LayoutCompositeTests
{
    private static readonly LayoutContext Context =
        new(PhotonTheme.Instance, ApproximateTextMeasurer.Instance);

    private static LayoutNode Layout(VisualNode root, LayoutNodePool? pool = null) =>
        LayoutEngine.Layout(root, 400, 300,
            pool is null ? Context
                : new LayoutContext(PhotonTheme.Instance, ApproximateTextMeasurer.Instance) { Pool = pool });

    private static IEnumerable<LayoutNode> Walk(LayoutNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static VisualNode Nested()
    {
        var inner = new Row(gap: Space.S1);
        inner.Add(new Text("two", TypeRole.BodyM));
        inner.Add(new Text("three", TypeRole.BodyM));
        var outer = new Column(gap: Space.S2);
        outer.Add(new Text("one", TypeRole.BodyM));
        outer.Add(inner);
        return outer;
    }

    [Fact]
    public void EveryChild_KnowsTheNodeThatPlacedIt()
    {
        var root = Layout(Nested());

        var orphans = Walk(root).Skip(1)
            .Where(node => node.Parent is null || !node.Parent.Children.Contains(node))
            .ToArray();

        orphans.Should().BeEmpty("a link held on one side only is the half that goes stale");
        Walk(root).Should().HaveCountGreaterThan(4, "the tree has to be real for this to mean anything");
    }

    [Fact]
    public void TheRoot_HasNoParent()
    {
        Layout(new Text("alone", TypeRole.BodyM)).Parent.Should().BeNull();
    }

    /// <summary>
    /// Walking UP reaches the root from anywhere — the property the one-way tree could not offer,
    /// and the reason ancestry was a string.
    /// </summary>
    [Fact]
    public void AnyNode_CanWalkHomeFromWhereItIs()
    {
        var root = Layout(Nested());
        var deepest = Walk(root).OrderByDescending(Depth).First();
        Depth(deepest).Should().BeGreaterThan(1, "the sample must actually nest");

        var climbed = deepest;
        while (climbed.Parent is { } up) climbed = up;
        climbed.Should().BeSameAs(root);

        static int Depth(LayoutNode node)
        {
            var depth = 0;
            for (var up = node.Parent; up is not null; up = up.Parent) depth++;
            return depth;
        }
    }

    /// <summary>
    /// The POOL is where a back-reference turns dangerous: a recycled node still pointing at last
    /// frame's parent is a tree that lies about itself, and nothing would say so. Reset clears both
    /// directions, and this is the assertion that keeps it doing so.
    /// </summary>
    [Fact]
    public void ARecycledNode_RemembersNoOne()
    {
        var pool = new LayoutNodePool();
        var root = Layout(Nested(), pool);
        var all = Walk(root).ToArray();
        all.Skip(1).Should().OnlyContain(n => n.Parent != null, "the tree was linked before recycling");

        pool.RecycleTree(root);

        all.Should().OnlyContain(n => n.Parent == null,
            "a pooled node carries nothing from the frame that let it go");
        root.Children.Should().BeEmpty();
    }
}
