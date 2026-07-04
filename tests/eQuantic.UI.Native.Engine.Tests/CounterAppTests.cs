using eQuantic.UI.Components.Shared;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The native Counter — the same app shape as the web sample, authored on the SHARED model:
/// a StatefulComponent whose Build composes shared components and abstract nodes; SetState
/// invalidates; the host rebuilds. Proves Build → tap → SetState → rebuild end-to-end on Photon.
/// </summary>
public class CounterAppTests
{
    /// <summary>Authored exactly like a web page component: state fields + SetState + Build.</summary>
    private sealed class CounterApp : StatefulComponent
    {
        private int _count;

        public int Count => _count;

        public void Increment() => SetState(() => _count++);

        public override VisualNode Build(ComponentContext context)
        {
            var theme = context.Theme;
            var primary = theme.Colors(Variant.Primary);

            var content = new Column(gap: Space.S3);
            content.Add(new Text("Interactive Counter", TypeRole.Title));
            content.Add(new Text($"Count: {_count}", TypeRole.BodyM, theme.TextSecondary));
            // A visual that GROWS with state — makes the rebuild visible in pixels/goldens.
            content.Add(new Box(new BoxStyle
            {
                Width = 24 + _count * 24,
                Height = 10,
                Background = primary.Base,
                CornerRadius = new CornerRadii(Radius.Full),
            }));
            content.Add(new Button("Increment", onPressed: Increment) { Key = "inc" });

            return new Box(new BoxStyle { Padding = EdgeInsets.All(Space.S4), Width = SizeValue.Fill },
                new Card(content, CardKind.Outlined) { Width = SizeValue.Fill });
        }
    }

    [Fact]
    public void TapIncrement_SetState_Rebuilds()
    {
        var app = new CounterApp();
        var host = new PhotonHost(app, PhotonTheme.Instance, ThemeMode.Light, 280, 220);

        host.NeedsRender.Should().BeTrue("first frame is always due");
        var frame = host.RenderFrame(new DisplayListBuilder());
        host.NeedsRender.Should().BeFalse();

        // Find the button's hit region and tap its center.
        frame.HitRegions.Should().HaveCount(1);
        var hit = frame.HitRegions[0].Bounds;
        host.Tap(hit.Center.X, hit.Center.Y).Should().BeTrue();

        app.Count.Should().Be(1, "SetState ran the mutation");
        host.NeedsRender.Should().BeTrue("SetState invalidated the host");

        // Rebuild: the state-driven bar is wider in the new layout.
        var before = FindBarWidth(frame.Root);
        var after = FindBarWidth(host.RenderFrame(new DisplayListBuilder()).Root);
        before.Should().Be(24);
        after.Should().Be(48, "the bar grows 24dp per count");
    }

    [Fact]
    public void Tap_OutsideAnyRegion_Misses()
    {
        var host = new PhotonHost(new CounterApp(), PhotonTheme.Instance, ThemeMode.Light, 280, 220);
        host.RenderFrame(new DisplayListBuilder());
        host.Tap(1, 1).Should().BeFalse();
    }

    private static float FindBarWidth(LayoutNode node)
    {
        // The growth bar is the only pill-radius Box in the tree.
        if (node.Source is Box { Style.CornerRadius.TopLeft: Radius.Full })
            return node.Bounds.Width;
        foreach (var child in node.Children)
        {
            var found = FindBarWidth(child);
            if (found > 0) return found;
        }
        return 0;
    }

    [Fact]
    public void CounterGoldens_BeforeAndAfterTaps()
    {
        var app = new CounterApp();
        var host = new PhotonHost(app, PhotonTheme.Instance, ThemeMode.Light, 280, 220);
        using var backend = new ReferenceBackend();

        Render(host, backend, "counter-initial");

        // Tap the increment button three times (hit region is stable across these frames).
        var frameBuilder = new DisplayListBuilder();
        var frame = host.RenderFrame(frameBuilder);
        var hit = frame.HitRegions[0].Bounds;
        host.Tap(hit.Center.X, hit.Center.Y);
        host.Tap(hit.Center.X, hit.Center.Y);
        host.Tap(hit.Center.X, hit.Center.Y);
        app.Count.Should().Be(3);

        Render(host, backend, "counter-after-3-taps");
    }

    private static void Render(PhotonHost host, ReferenceBackend backend, string golden)
    {
        using var surface = backend.CreateSurface((int)host.Width, (int)host.Height);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        backend.Render(builder.Build(), surface);
        GoldenImage.Match(surface, golden);
    }
}
