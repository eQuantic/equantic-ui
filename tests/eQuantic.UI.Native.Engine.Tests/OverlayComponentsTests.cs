using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Spec C3/C4 — Toast and BottomSheet on Photon pixels, plus the MODALITY contract: a Toast layer
/// never blocks the page behind it (only its own action takes input); a BottomSheet's scrim does.
/// </summary>
public class OverlayComponentsTests
{
    private static VisualNode Page(VisualNode overlay, Action? pageAction = null)
    {
        var column = new Column(gap: Space.S3) { Padding = EdgeInsets.All(Space.S3), Width = SizeValue.Fill };
        column.Add(new Button("Page action", onPressed: pageAction));
        column.Add(new Text("Content behind the layer", TypeRole.BodyM));
        column.Add(overlay);
        return column;
    }

    [Theory]
    [InlineData(ThemeMode.Light, "toast-light")]
    [InlineData(ThemeMode.Dark, "toast-dark")]
    public void Toast_RendersThePhotonPixels(ThemeMode mode, string golden)
    {
        var toast = new Toast("Draft saved", Variant.Success, "Undo", () => { });
        Render(Page(toast), mode, golden);
    }

    [Theory]
    [InlineData(ThemeMode.Light, "bottom-sheet-light")]
    [InlineData(ThemeMode.Dark, "bottom-sheet-dark")]
    public void BottomSheet_RendersThePhotonPixels(ThemeMode mode, string golden)
    {
        var content = new Column(gap: Space.S2);
        content.Add(new Text("Share this file", TypeRole.Title));
        content.Add(new ListItem("Copy link", "Anyone with the link can view"));
        content.Add(new ListItem("Email", "Send a copy"));
        var sheet = new BottomSheet(content, onDismiss: () => { });
        Render(Page(sheet), mode, golden);
    }

    [Fact]
    public void Toast_IsNonModal_ButItsActionTakesInput()
    {
        var pagePressed = false;
        var undone = false;
        var toast = new Toast("Draft saved", Variant.Success, "Undo", () => undone = true);
        var host = new PhotonHost(Page(toast, () => pagePressed = true),
            PhotonTheme.Instance, ThemeMode.Light, 360, 400);
        var frame = host.RenderFrame(new DisplayListBuilder());

        // The page button region is reachable THROUGH the toast layer (non-modal).
        var pageRegion = frame.HitRegions[0];
        host.PressDown(pageRegion.Bounds.Center.X, pageRegion.Bounds.Center.Y);
        host.PressUp(pageRegion.Bounds.Center.X, pageRegion.Bounds.Center.Y);
        pagePressed.Should().BeTrue("a toast never blocks the page behind it");

        // The toast's own action still takes input (topmost-last dispatch).
        var undoRegion = frame.HitRegions[^1];
        host.PressDown(undoRegion.Bounds.Center.X, undoRegion.Bounds.Center.Y);
        host.PressUp(undoRegion.Bounds.Center.X, undoRegion.Bounds.Center.Y);
        undone.Should().BeTrue();
    }

    [Fact]
    public void BottomSheet_Scrim_BlocksThePage_AndDismisses()
    {
        var pagePressed = false;
        var dismissed = false;
        var sheet = new BottomSheet(new Text("body", TypeRole.BodyM), onDismiss: () => dismissed = true);
        var host = new PhotonHost(Page(sheet, () => pagePressed = true),
            PhotonTheme.Instance, ThemeMode.Light, 360, 400);
        var frame = host.RenderFrame(new DisplayListBuilder());

        // Pressing where the page button sits hits the SCRIM (topmost) — dismiss fires, page never does.
        var pageRegion = frame.HitRegions[0];
        host.PressDown(pageRegion.Bounds.Center.X, pageRegion.Bounds.Center.Y);
        host.PressUp(pageRegion.Bounds.Center.X, pageRegion.Bounds.Center.Y);
        pagePressed.Should().BeFalse("the modal scrim blocks everything behind it");
        dismissed.Should().BeTrue("a dismissible sheet resolves on scrim tap");
    }

    private static void Render(VisualNode root, ThemeMode mode, string golden)
    {
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(360, 400);
        var host = new PhotonHost(root, PhotonTheme.Instance, mode, 360, 400);
        // Two frames: the first (t=0) starts the enter motion; the golden pins the SETTLED state
        // (t past Motion.Base) — byte-identical to the pre-motion goldens, proving the entrance
        // leaves no residue at rest.
        host.RenderFrame(new DisplayListBuilder());
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, timeMs: 1000);
        backend.Render(builder.Build(), surface);
        GoldenImage.Match(surface, golden);
    }
}
