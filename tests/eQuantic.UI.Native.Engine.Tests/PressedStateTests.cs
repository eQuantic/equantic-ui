using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>Interaction slice 1: press tracking on the host + the §01 pressed token swap.</summary>
public class PressedStateTests
{
    private static PhotonHost Host(out DisplayListBuilder builder)
    {
        var row = new Row(gap: Space.S3) { Padding = EdgeInsets.All(Space.S4) };
        row.Add(new Button("Save", onPressed: () => { }));
        row.Add(new Button("Ghost", Variant.Ghost));
        var host = new PhotonHost(row, PhotonTheme.Instance, ThemeMode.Light, 240, 80);
        builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        return host;
    }

    [Fact]
    public void PressDown_MarksPressed_AndInvalidates()
    {
        var host = Host(out _);
        host.PressDown(40, 36).Should().BeTrue();
        host.Pressed.Should().NotBeNull();
        host.NeedsRender.Should().BeTrue();
    }

    [Fact]
    public void PressUp_InsideFires_OutsideCancels()
    {
        var fired = 0;
        var button = new Button("Go", onPressed: () => fired++);
        var box = new Primitives.Box(new BoxStyle { Padding = EdgeInsets.All(Space.S4) }, button);
        var host = new PhotonHost(box, PhotonTheme.Instance, ThemeMode.Light, 160, 80);
        host.RenderFrame(new DisplayListBuilder());

        host.PressDown(40, 36);
        host.PressUp(40, 36).Should().BeTrue();
        fired.Should().Be(1);

        host.PressDown(40, 36);
        host.PressUp(150, 75).Should().BeFalse("releasing outside cancels");
        fired.Should().Be(1);
        host.Pressed.Should().BeNull();
    }

    [Fact]
    public void PressedFrame_Golden()
    {
        var host = Host(out _);
        host.PressDown(40, 36);

        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(240, 80);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        backend.Render(builder.Build(), surface);
        GoldenImage.Match(surface, "pressed-button");
    }
}
