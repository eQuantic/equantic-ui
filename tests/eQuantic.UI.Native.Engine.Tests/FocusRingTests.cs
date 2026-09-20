using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>Interaction slice 2: focus traversal on the host + the §01 double ring.</summary>
public class FocusRingTests
{
    private static PhotonHost Host()
    {
        var row = new Row(gap: Space.S4) { Padding = EdgeInsets.All(Space.S4) };
        row.Add(new Button("Save", onPressed: () => { }));
        row.Add(new Button("Disabled") { Disabled = true });
        row.Add(new Button("Ghost", Variant.Ghost, onPressed: () => { }));
        var host = new PhotonHost(row, PhotonTheme.Instance, ThemeMode.Light, 300, 80);
        host.RenderFrame(new DisplayListBuilder());
        return host;
    }

    [Fact]
    public void FocusNext_CyclesEnabledPressables_SkippingDisabled()
    {
        var host = Host();
        host.FocusNext().Should().BeTrue();
        var first = host.Focused;
        first.Should().NotBeNull();

        host.FocusNext();
        var second = host.Focused;
        second.Should().NotBeSameAs(first);
        second!.Disabled.Should().BeFalse("disabled pressables are skipped");

        host.FocusNext();
        host.Focused.Should().BeSameAs(first, "traversal wraps");

        host.ClearFocus();
        host.Focused.Should().BeNull();
    }

    /// <summary>
    /// The ring is DRAWN, not merely tracked — and its golden was recorded without it, so this says
    /// it in a way pixels cannot quietly bless.
    /// </summary>
    [Fact]
    public void FocusingActuallyPaintsTheRing()
    {
        // Counted against the SAME frame unfocused, rather than looked up by colour: the ring's
        // token and the primary button's fill are both #0050A0, so "a command of that colour exists"
        // is true of a frame with no ring in it at all.
        var host = Host();
        var before = Paint(host);

        host.FocusNext();
        var after = Paint(host);

        after.Length.Should().BeGreaterThan(before.Length,
            "a keyboard user has nothing but the ring to tell them where they are");
    }

    private static DrawCommand[] Paint(PhotonHost host)
    {
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        return builder.Build().Commands.ToArray();
    }

    /// <summary>
    /// …and it stays away from the mouse. Focus and its ring are different things: someone who
    /// clicked a button knows what they clicked, and a ring left behind on every click reads as a
    /// glitch. Same rule as `:focus-visible` on the web, same reason.
    /// </summary>
    [Fact]
    public void ClickingDoesNotLeaveARingBehind()
    {
        var host = Host();
        var before = Paint(host);

        host.PressDown(40, 36);
        host.PressUp(40, 36);
        var after = Paint(host);

        after.Length.Should().Be(before.Length, "a click must not leave a ring behind");
        host.Focused.Should().NotBeNull("the button still holds focus — Enter after a click must work");
    }

    /// <summary>
    /// A LINK is words, not a box — and the ring is drawn by the first Box under the focused
    /// control. So the moment a link became focusable (#255), the ring it never claimed would have
    /// been taken by whatever box the tree emitted NEXT: a ring around the button below, while the
    /// keyboard sat on the link above it. The link arm draws its own and clears the pending flag.
    /// <para>Mutation: delete that arm and the ring moves to the button, failing both halves.</para>
    /// </summary>
    [Fact]
    public void AFocusedLinkRingsItselfAndNotTheBoxAfterIt()
    {
        var page = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        page.Add(new Link("/home", new Text("home", TypeRole.Label)));
        page.Add(new Button("Save", onPressed: () => { }));

        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 300, 120);
        var frame = host.RenderFrame(new DisplayListBuilder());
        var link = frame.FocusStops.Single(stop => stop.Destination is not null);
        var button = frame.FocusStops.Single(stop => stop.Pressable is not null);

        host.KeyDown("Tab").Should().BeTrue();
        var strokes = Paint(host).Where(command => command.StrokeWidth > 0).ToArray();

        strokes.Should().Contain(command => Rings(command.Shape, link.Bounds),
            "a keyboard user has nothing but the ring to tell them where they are");
        strokes.Should().NotContain(command => Rings(command.Shape, button.Bounds),
            "the ring belongs to what Tab landed on, not to the next control that happens to have a box");
    }

    /// <summary>A stroke that sits OUTSIDE a control, centred on it: the §01 ring rather than the
    /// control's own border, which shares its rectangle exactly.</summary>
    private static bool Rings(RRect shape, Rect control) =>
        MathF.Abs(shape.Rect.Center.X - control.Center.X) < 0.5f
        && MathF.Abs(shape.Rect.Center.Y - control.Center.Y) < 0.5f
        && shape.Rect.Width > control.Width;

    [Fact]
    public void FocusedFrame_Golden()
    {
        var host = Host();
        host.FocusNext();

        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(300, 80);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        backend.Render(builder.Build(), surface);
        GoldenImage.Match(surface, "focus-ring");
    }
}
