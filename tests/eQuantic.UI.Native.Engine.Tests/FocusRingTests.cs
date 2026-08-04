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
