using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A <see cref="DropTarget"/> on the server: the same layout-transparent div a <see cref="Hoverable"/>
/// lowers to, marked as a drop area, declaring the four drag events the browser dispatches and the
/// client twin answers (<c>drop-target.spec.ts</c>, <c>file-drop.spec.ts</c>). Photon renders the child
/// and takes nothing yet (#1216).
/// </summary>
public class DropTargetRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    [Fact]
    public void ADropArea_IsATransparentDiv_ThatDeclaresTheDragEvents()
    {
        var element = WebRealizer.Lower(new DropTarget(new Box(new BoxStyle { Width = 10, Height = 10 }), _ => { }), Theme);

        var area = element.Should().BeOfType<RealizedElement>().Subject;
        area.Tag.Should().Be("div");
        area.RawAttributes.Should().Contain("data-eq-drop", "on");
        element.CustomEvents.Keys.Should().BeEquivalentTo("dragenter", "dragover", "dragleave", "drop");
        element.Children.Should().ContainSingle();
    }

    [Fact]
    public void ADisabledDropArea_SaysItIsOff()
    {
        var element = WebRealizer.Lower(
            new DropTarget(new Box(new BoxStyle { Width = 10, Height = 10 }), _ => { }) { Enabled = false }, Theme);

        element.Should().BeOfType<RealizedElement>().Subject
            .RawAttributes.Should().Contain("data-eq-drop", "off");
    }

    [Fact]
    public void AFillChild_PassesThePercentChainThrough()
    {
        var element = WebRealizer.Lower(
            new DropTarget(new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill }), _ => { }), Theme);

        element.Style!.Width.Should().Be("100%");
        element.Style.Height.Should().Be("100%");
        element.Style.PointerEvents.Should().Be("auto");
    }
}
