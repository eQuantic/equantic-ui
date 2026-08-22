using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The native half of the build-ROOT retention contract. A composed component whose Build returns
/// a stateful component AS ITS ROOT — <c>CultureSwitcher</c> returns a <c>Menu</c>, a search box
/// returns a combo — must keep that component's state across the frame its own SetState asks for.
/// <para>
/// The web half broke on exactly this: its render path called <c>.render()</c> on the build root
/// instead of resolving it against the positional store, so the root was a fresh instance every
/// pass and a menu closed in the same frame it opened. The engine takes one road for every node,
/// which is why it never did — and this test is what says so out loud, so a future short-cut on
/// the native side fails here instead of in a window.
/// </para>
/// </summary>
public class BuildRootRetentionTests
{
    /// <summary>The smallest thing whose state is visible: a count and a press that raises it.</summary>
    private sealed class Counter : StatefulComponent
    {
        private int _count;
        public int Count => _count;

        public override VisualNode Build(ComponentContext context)
        {
            var row = new Row(gap: Space.S2);
            row.Add(new Text($"count {_count}", TypeRole.Caption));
            row.Add(new Button("+", onPressed: () => SetState(() => _count++)));
            return row;
        }
    }

    /// <summary>A stateless composition rooted ON the counter — the shape that failed on the web.</summary>
    private sealed class RootIsTheCounter : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Counter();
    }

    /// <summary>The same counter one level down — the position that always worked.</summary>
    private sealed class CounterNestedInAColumn : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2);
            column.Add(new Counter());
            return column;
        }
    }

    [Fact]
    public void AStatefulBuildRoot_KeepsItsStateAcrossItsOwnRerender()
    {
        var host = new PhotonHost(new RootIsTheCounter(), PhotonTheme.Instance, ThemeMode.Light, 300, 200);
        host.RenderFrame(new DisplayListBuilder());

        Press(host);
        Press(host);

        FindCounter(host).Count.Should().Be(2, "the build ROOT is retained, like every component below it");
    }

    [Fact]
    public void TheSameCounterNestedOneLevelDown_BehavesIdentically()
    {
        var host = new PhotonHost(new CounterNestedInAColumn(), PhotonTheme.Instance, ThemeMode.Light, 300, 200);
        host.RenderFrame(new DisplayListBuilder());

        Press(host);
        Press(host);

        FindCounter(host).Count.Should().Be(2, "root and nested must not be two different contracts");
    }

    /// <summary>Presses the + button wherever the last frame put it, then draws the next frame.</summary>
    private static void Press(PhotonHost host)
    {
        var frame = host.RenderFrame(new DisplayListBuilder());
        var button = Find(frame.Root, node => node.Source is Button);
        button.Should().NotBeNull("the counter draws a + button");
        var bounds = button!.Bounds;
        host.Tap(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        host.RenderFrame(new DisplayListBuilder());
    }

    private static Counter FindCounter(PhotonHost host)
    {
        var frame = host.RenderFrame(new DisplayListBuilder());
        var node = Find(frame.Root, candidate => candidate.Source is Counter);
        node.Should().NotBeNull();
        return (Counter)node!.Source!;
    }

    private static eQuantic.UI.Native.Framework.LayoutNode? Find(
        eQuantic.UI.Native.Framework.LayoutNode node,
        Func<eQuantic.UI.Native.Framework.LayoutNode, bool> predicate)
    {
        if (predicate(node)) return node;
        foreach (var child in node.Children)
        {
            var found = Find(child, predicate);
            if (found is not null) return found;
        }
        return null;
    }
}
