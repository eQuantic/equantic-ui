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
        public float Reported = -1;

        public void Ask(ScrollRequest? request) => SetState(() => _request = request);

        public override VisualNode Build(ComponentContext context)
        {
            var rows = new Column(gap: 0) { Width = SizeValue.Fill };
            for (var i = 0; i < 40; i++) rows.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 20 }));
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
    }
}
