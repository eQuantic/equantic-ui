using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// An anchored panel its placement would put past the viewport's start or end edge is moved back inside,
/// <see cref="Anchored.ViewportInset"/> from the edge, and a date picker hangs its calendar under the end of its
/// field (#1933). On a field that spans its column, the calendar hung from the opener's start and left the viewport at
/// every width: 306 to 640 in a 390 viewport, 669 to 1003 in 768, 955 to 1289 in 1280.
/// </summary>
public class AnAnchoredPanelStaysInTheViewportTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    /// <summary>A date picker whose field spans the viewport, as an order form lays it out, with its calendar open.</summary>
    private static PhotonHost OpenPicker(float width)
    {
        var page = new Box(new BoxStyle { Width = SizeValue.Fill, Padding = EdgeInsets.All(16) },
            new DatePicker(new DateOnly(2026, 11, 27), _ => { }, label: "Delivery"));
        var host = new PhotonHost(page, Theme, ThemeMode.Light, width, 900);
        host.RenderFrame(new DisplayListBuilder());
        var opener = host.Semantics().Single(node => node.Expanded is not null);
        host.Activate(opener.Id).Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder(), 16);
        return host;
    }

    /// <summary>The open panel: the last child of the last layer, where the anchored node places it.</summary>
    private static Rect Panel(PhotonHost host) => host.LastFrame!.OverlayRoots[^1].Children[^1].Bounds;

    [Theory]
    [InlineData(390)]
    [InlineData(768)]
    [InlineData(1280)]
    public void ADatePickersCalendar_StaysInsideTheViewport(float width)
    {
        var panel = Panel(OpenPicker(width));

        panel.Width.Should().BeGreaterThan(200, "the calendar is the panel");
        panel.X.Should().BeGreaterThanOrEqualTo(Anchored.ViewportInset, "its start stays inside the viewport");
        panel.Right.Should().BeLessThanOrEqualTo(width - Anchored.ViewportInset, "and so does its end");
    }

    [Fact]
    public void ADatePickersCalendar_HangsUnderTheEndOfItsField()
    {
        var host = OpenPicker(1280);
        var opener = host.LastFrame!.HitRegions.Single(region => region.Node.Label == SdkStrings.ChooseDate).Drawn;

        Panel(host).Right.Should().BeApproximately(opener.Right, 0.5f, "BottomEnd: the calendar's end under the opener's");
    }

    /// <summary>Any anchored panel, not only a date picker's: a menu opened from the start edge with its end
    /// placement, and a panel wider than the viewport, which starts at the start edge's inset.</summary>
    [Theory]
    [InlineData(300f, 360f, 8f)]
    [InlineData(500f, 360f, 8f)]
    public void AnAnchoredPanelPastAnEdge_ComesBackInside(float panelWidth, float viewport, float expectedStart)
    {
        var anchored = new Anchored(new Box(new BoxStyle { Width = 40, Height = 40 }),
            new Box(new BoxStyle { Width = panelWidth, Height = 120 }))
        {
            Open = true,
            Placement = AnchorPlacement.BottomEnd,
        };
        var host = new PhotonHost(new Box(new BoxStyle { Width = SizeValue.Fill, Padding = EdgeInsets.All(16) }, anchored),
            Theme, ThemeMode.Light, viewport, 600);
        host.RenderFrame(new DisplayListBuilder());
        host.RenderFrame(new DisplayListBuilder(), 16);

        Panel(host).X.Should().Be(expectedStart, "hung from a 40dp anchor at the start, the panel's end placement put it past the start edge");
    }
}
