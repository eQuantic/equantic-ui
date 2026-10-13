using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A host takes its tree down when the surface it drew for is gone for good. A phone ends an
/// activity and keeps the process: with nothing unmounted, the tree of every activity the process
/// had stayed subscribed to its services, and a link opened later was heard once per dead tree as
/// well as by the live one (#777).
/// </summary>
public class HostUnmountTests
{
    private sealed class Leaf(List<string> log) : StatefulComponent
    {
        protected override void OnMount() => log.Add("leaf mounted");
        protected override void OnUnmount() => log.Add("leaf unmounted");
        public override VisualNode Build(ComponentContext context) => new Text("leaf", TypeRole.Caption);
    }

    private sealed class Root(List<string> log) : StatefulComponent
    {
        protected override void OnMount() => log.Add("root mounted");
        protected override void OnUnmount() => log.Add("root unmounted");

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2);
            column.Add(new Leaf(log));
            return column;
        }
    }

    [Fact]
    public void EveryComponentThatMounted_IsToldItLeft()
    {
        var log = new List<string>();
        var host = new PhotonHost(new Root(log), PhotonTheme.Instance, ThemeMode.Light, 300, 200);
        host.RenderFrame(new DisplayListBuilder());
        log.Should().BeEquivalentTo(["root mounted", "leaf mounted"]);

        host.Unmount();

        log.Skip(2).Should().BeEquivalentTo(["root unmounted", "leaf unmounted"], "each one once, the root included");
    }

    [Fact]
    public void TakenDownTwice_NobodyIsToldTwice()
    {
        var log = new List<string>();
        var host = new PhotonHost(new Root(log), PhotonTheme.Instance, ThemeMode.Light, 300, 200);
        host.RenderFrame(new DisplayListBuilder());

        host.Unmount();
        host.Unmount();

        log.Count(line => line.EndsWith("unmounted", StringComparison.Ordinal)).Should().Be(2);
    }

    /// <summary>A host that never drew mounted nothing, so there is nothing to tell.</summary>
    [Fact]
    public void AHostThatNeverDrew_HasNothingToTakeDown()
    {
        var log = new List<string>();
        new PhotonHost(new Root(log), PhotonTheme.Instance, ThemeMode.Light, 300, 200).Unmount();

        log.Should().BeEmpty();
    }

    /// <summary>The point of it: what a component subscribed to stops calling it.</summary>
    [Fact]
    public void ASubscriptionTheTreeHeld_StopsHearing()
    {
        var relay = new DeepLinkRelay();
        var heard = new List<Uri>();
        var host = new PhotonHost(new Listener(relay, heard), PhotonTheme.Instance, ThemeMode.Light, 300, 200);
        host.RenderFrame(new DisplayListBuilder());
        relay.Offer("acme://one");

        host.Unmount();
        relay.Offer("acme://two");

        heard.Should().Equal(new Uri("acme://one"));
    }

    private sealed class Listener(IDeepLinks links, List<Uri> heard) : StatefulComponent
    {
        private IDisposable? _subscription;
        protected override void OnMount() => _subscription = links.Subscribe(heard.Add);
        protected override void OnUnmount() => _subscription?.Dispose();
        public override VisualNode Build(ComponentContext context) => new Text("listening", TypeRole.Caption);
    }
}
