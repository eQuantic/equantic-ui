using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A scroll view goes where the app asks, once per request (<see cref="ScrollView.Request"/>, Flutter's
/// <c>ScrollController.jumpTo</c>, #1083). Its <see cref="ScrollView.Offset"/> is read until the view is
/// first scrolled and never after, so before the request no component could move its own view again.
/// </summary>
public class ScrollRequestTests
{
    private const float Width = 300;
    private const float Height = 200;

    /// <summary>A list of forty rows of 20, 800 of content in 200, built with whatever request the test
    /// hands it last.</summary>
    private sealed class Asked : StatefulComponent
    {
        private ScrollRequest? _request;
        private int _rows = 40;
        public float Reported = -1;

        public void Ask(ScrollRequest? request) => SetState(() => _request = request);

        public void Grow(int rows) => SetState(() => _rows = rows);

        public override VisualNode Build(ComponentContext context)
        {
            var rows = new Column(gap: 0) { Width = SizeValue.Fill };
            for (var i = 0; i < _rows; i++) rows.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 20 }));
            return new ScrollView(rows)
            {
                Height = SizeValue.Fill,
                Request = _request,
                OnScrolled = offset => Reported = offset,
            };
        }
    }

    private static LayoutNode? TryFind<T>(LayoutNode node) where T : VisualNode
    {
        if (node.Source is T) return node;
        foreach (var child in node)
            if (TryFind<T>(child) is { } found) return found;
        return null;
    }

    private static float ScrolledBy(RealizeResult frame)
    {
        var view = TryFind<ScrollView>(frame.Root) ?? throw new InvalidOperationException("No scroll view.");
        return view.Bounds.Y - view.Children[0].Bounds.Y;
    }

    [Fact]
    public void ARequest_IsHonouredOncePerVersion()
    {
        var asked = new Asked();
        var host = new PhotonHost(asked, PhotonTheme.Instance, ThemeMode.Light, Width, Height) { SmoothScroll = false };
        ScrolledBy(host.RenderFrame(new DisplayListBuilder())).Should().Be(0);

        asked.Ask(new ScrollRequest(1) { Offset = 300 });
        ScrolledBy(host.RenderFrame(new DisplayListBuilder())).Should().Be(300, "a request after the first frame moves the view");
        asked.Reported.Should().Be(300);

        host.ScrollBy(Width / 2, Height / 2, 0, -100).Should().BeTrue();
        ScrolledBy(host.RenderFrame(new DisplayListBuilder())).Should().Be(200, "the reader scrolled after the request");
        asked.Ask(new ScrollRequest(1) { Offset = 300 });
        ScrolledBy(host.RenderFrame(new DisplayListBuilder()))
            .Should().Be(200, "a request already honoured moves nothing, however often it is built");

        asked.Ask(new ScrollRequest(2) { Offset = 5000 });
        ScrolledBy(host.RenderFrame(new DisplayListBuilder())).Should().Be(600, "a request past the end goes to the end");
        asked.Reported.Should().Be(600, "and says where it went");
        host.ScrollBy(Width / 2, Height / 2, 0, -100).Should().BeTrue();
        ScrolledBy(host.RenderFrame(new DisplayListBuilder())).Should().Be(500, "the wheel moves from the end it stands at");
    }

    /// <summary>A request past the end settles at the end the frame's layout gives it (Copilot on #1091): kept
    /// as asked, it moved the view again when the content grew, with no request and no hand on it.</summary>
    [Fact]
    public void ARequestPastTheEnd_StaysAtTheEndWhenTheContentGrows()
    {
        var asked = new Asked();
        var host = new PhotonHost(asked, PhotonTheme.Instance, ThemeMode.Light, Width, Height) { SmoothScroll = false };
        host.RenderFrame(new DisplayListBuilder());
        asked.Ask(new ScrollRequest(1) { Offset = 5000 });
        ScrolledBy(host.RenderFrame(new DisplayListBuilder())).Should().Be(600);

        asked.Grow(80);

        ScrolledBy(host.RenderFrame(new DisplayListBuilder())).Should().Be(600, "the view stays where it stood");
        host.Resize(Width, Height - 100);
        ScrolledBy(host.RenderFrame(new DisplayListBuilder())).Should().Be(600, "and a resize does not move it either");
    }

    /// <summary>A wheel that glides after a request past the end glides from the end (Copilot on #1091): the
    /// glide started from the offset as asked, and the view stayed at the end until it had crossed the
    /// excess.</summary>
    [Fact]
    public void AGlideAfterARequestPastTheEnd_StartsFromTheEnd()
    {
        var asked = new Asked();
        var host = new PhotonHost(asked, PhotonTheme.Instance, ThemeMode.Light, Width, Height) { SmoothScroll = true };
        var now = 1000f;
        host.RenderFrame(new DisplayListBuilder(), now);
        asked.Ask(new ScrollRequest(1) { Offset = 5000 });
        ScrolledBy(host.RenderFrame(new DisplayListBuilder(), now += 16)).Should().Be(600);

        host.ScrollBy(Width / 2, Height / 2, 0, -100).Should().BeTrue();
        ScrolledBy(host.RenderFrame(new DisplayListBuilder(), now += 16))
            .Should().BeLessThan(600, "the glide leaves the end on its first frame");
        for (var i = 0; i < 60; i++) host.RenderFrame(new DisplayListBuilder(), now += 16);
        ScrolledBy(host.RenderFrame(new DisplayListBuilder(), now += 16)).Should().BeApproximately(500, 1);
    }
}
