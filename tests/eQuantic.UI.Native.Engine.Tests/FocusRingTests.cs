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
