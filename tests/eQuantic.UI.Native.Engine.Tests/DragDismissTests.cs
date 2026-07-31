using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Gestures v2 — drag-to-dismiss (the sheet contract): a press inside a <see cref="DragDismiss"/>
/// surface becomes a DRAG past the slop (cancelling the pressable press — taps inside keep
/// working), the subtree follows the pointer as a paint-only translate, and release resolves:
/// past the threshold OnDismiss fires (state removes the subtree and the presence EXIT completes
/// from the dragged position); short of it the offset glides back over Motion.Base.
/// </summary>
public class DragDismissTests
{
    // ---- The store ------------------------------------------------------------------------------

    [Fact]
    public void Store_FollowsRaw_GlidesBackOnRelease_AndDropsOnDismiss()
    {
        var store = new DragStore();

        store.BeginFrame();
        store.Drag("s", 40);
        store.Resolve("s", 0).Should().Be(40f, "an active drag follows raw");
        store.AnyActive.Should().BeFalse("input marks the frame dirty — a held finger burns no frames");

        store.Release("s", 1000);
        store.BeginFrame();
        store.Resolve("s", 1100).Should().Be(20f, "smoothstep midpoint of the Base glide-back");
        store.AnyActive.Should().BeTrue("the glide keeps frames scheduled");

        store.BeginFrame();
        store.Resolve("s", 1300).Should().Be(0f, "landed");
        store.AnyActive.Should().BeFalse();

        store.Drag("s", 30);
        store.Drop("s");
        store.Resolve("s", 0).Should().Be(0f, "a dismissal clears the offset outright");
    }

    [Fact]
    public void Store_ClampsUpwardDrags_AtRest()
    {
        var store = new DragStore();
        store.Drag("s", -25);
        store.Resolve("s", 0).Should().Be(0f, "a sheet never drags UP past its rest position");
    }

    // ---- The host state machine -----------------------------------------------------------------

    private sealed class SheetPage : Primitives.StatefulComponent
    {
        public bool Open = true;
        public int ActionPresses;
        public int Dismissals;

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: 0) { Width = SizeValue.Fill };
            column.Add(new Text("Page", TypeRole.BodyM));
            if (Open)
            {
                var content = new Column(gap: Space.S2);
                content.Add(new Button("Act", onPressed: () => ActionPresses++));
                column.Add(new BottomSheet(content,
                    onDismiss: () => SetState(() =>
                    {
                        Dismissals++;
                        Open = false;
                    })));
            }
            return column;
        }
    }

    private static (PhotonHost Host, SheetPage Page, RealizeResult Frame) SettledSheet(ThemeMode mode = ThemeMode.Light)
    {
        var page = new SheetPage();
        var host = new PhotonHost(page, PhotonTheme.Instance, mode, 360, 400);
        host.RenderFrame(new DisplayListBuilder());
        var frame = host.RenderFrame(new DisplayListBuilder(), 1000);
        return (host, page, frame);
    }

    [Fact]
    public void DismissibleSheet_RegistersADragRegion_TopmostUnderThePoint()
    {
        var (_, _, frame) = SettledSheet();
        frame.DragRegions.Should().ContainSingle("the dismissible sheet is the one drag surface");
        frame.DragRegions[0].Path.Should().StartWith("ov", "the sheet lives in the overlay pass");
    }

    [Fact]
    public void TapInsideTheSheet_WithoutTravel_StillPresses()
    {
        var (host, page, frame) = SettledSheet();
        var button = frame.HitRegions.Single(r => r.Node.Label == "Act");

        host.PressDown(button.Bounds.Center.X, button.Bounds.Center.Y);
        host.PressUp(button.Bounds.Center.X, button.Bounds.Center.Y);

        page.ActionPresses.Should().Be(1, "a tap without slop travel is a tap — the drag never engages");
        page.Dismissals.Should().Be(0);
    }

    [Fact]
    public void DragPastSlop_FollowsThePointer_AndCancelsThePress()
    {
        var (host, page, frame) = SettledSheet();
        var button = frame.HitRegions.Single(r => r.Node.Label == "Act");
        var x = button.Bounds.Center.X;
        var y = button.Bounds.Center.Y;

        host.PressDown(x, y);
        host.PointerMove(x, y + 40);                      // past the 12dp slop → drag activates
        host.Pressed.Should().BeNull("activating the drag cancels the pressable press");

        var mid = new DisplayListBuilder();
        host.RenderFrame(mid, 1100);
        mid.Build().Commands.ToArray()
            .Should().Contain(c => c.Transform == Matrix2D.Translation(0, 40),
                "the sheet follows the pointer as a paint-only translate");

        host.PressUp(x, y + 40);                          // short of the 96dp threshold
        page.ActionPresses.Should().Be(0, "the cancelled press never fires");
        page.Dismissals.Should().Be(0, "short of the threshold does not dismiss");

        // The glide-back: mid-flight offset between 40 and 0, frames stay scheduled.
        var glide = new DisplayListBuilder();
        var glideFrame = host.RenderFrame(glide, 1200);   // 100ms into the Base glide
        glide.Build().Commands.ToArray()
            .Should().Contain(c => c.Transform == Matrix2D.Translation(0, 20), "smoothstep midpoint");
        glideFrame.HasActiveMotion.Should().BeTrue();
    }

    [Fact]
    public void DragPastThreshold_Dismisses_AndTheExitCompletesFromTheDraggedPosition()
    {
        var (host, page, frame) = SettledSheet();
        var region = frame.DragRegions[0];
        var x = region.Bounds.Center.X;
        var y = region.Bounds.Center.Y;

        host.PressDown(x, y);
        host.PointerMove(x, y + 120);                     // past ThresholdDp (96)
        host.RenderFrame(new DisplayListBuilder(), 1100); // dragged frame (snapshot carries the offset)
        host.PressUp(x, y + 120);

        page.Dismissals.Should().Be(1, "past the threshold the release dismisses");

        // The subtree left the tree — the presence EXIT replays it (falling layer), input dead.
        host.RenderFrame(new DisplayListBuilder(), 1150); // departure frame — exit starts
        var exiting = new DisplayListBuilder();
        var exitFrame = host.RenderFrame(exiting, 1200);
        exiting.Build().Commands.ToArray()
            .Should().Contain(c => c.Kind == DrawCommandKind.BeginLayer && c.StrokeWidth == 0.5f,
                "the dragged sheet fades out through the presence exit");
        exitFrame.DragRegions.Should().BeEmpty("the departed surface no longer takes drags");
    }

    // ---- The drag itself, as pixels -------------------------------------------------------------

    [Theory]
    [InlineData(ThemeMode.Light, "sheet-dragging-light")]
    [InlineData(ThemeMode.Dark, "sheet-dragging-dark")]
    public void BottomSheet_MidDrag_RendersTheFollowedSheet(ThemeMode mode, string golden)
    {
        var (host, _, frame) = SettledSheet(mode);
        var region = frame.DragRegions[0];
        host.PressDown(region.Bounds.Center.X, region.Bounds.Center.Y);
        host.PointerMove(region.Bounds.Center.X, region.Bounds.Center.Y + 40);

        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(360, 400);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 1100);
        backend.Render(builder.Build(), surface);

        GoldenImage.Match(surface, golden);
    }
}
