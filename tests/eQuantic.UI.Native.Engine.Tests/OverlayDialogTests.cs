using eQuantic.UI.Components.Shared;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The overlay infrastructure (Phase C slice 1) and its first consumer, the Dialog (spec C2):
/// an Overlay is ZERO in the page flow — the realizer lays its child out against the VIEWPORT and
/// paints it AFTER the page (painter's order), registering its hit regions last so the topmost-
/// last-wins dispatch routes taps to the layer. The scrim is an ordinary full-viewport Pressable
/// composed by the component: armed with OnDismiss when dismissible, otherwise DISABLED — swallowing
/// taps without reacting (the destructive-confirm contract).
/// </summary>
public class OverlayDialogTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static DialogAction[] ConfirmActions(Action? keep = null, Action? delete = null) =>
    [
        new("Keep card", keep, Variant.Ghost),
        new("Delete", delete, Variant.Destructive),
    ];

    private static VisualNode PageWithDialog(bool dialog = true, Action? pageTap = null,
        bool dismissible = false, Action? onDismiss = null)
    {
        var column = new Column(gap: Space.S3) { Width = SizeValue.Fill };
        column.Add(new Text("Cards", TypeRole.Title));
        column.Add(new Button("Open", onPressed: pageTap));
        if (dialog)
        {
            column.Add(new Dialog("Delete card?",
                "\"Work Visa 4821\" will be removed and its automatic payments will stop.",
                ConfirmActions(), dismissible, onDismiss));
        }
        return column;
    }

    [Fact]
    public void Overlay_PaintsAboveThePage_AndCoversTheViewport()
    {
        var builder = new DisplayListBuilder();
        PhotonRealizer.Realize(PageWithDialog(), 360, 480, Theme, ThemeMode.Light, builder);
        var commands = builder.Build().Commands.ToArray();

        // The scrim fill covers the viewport and paints AFTER every page command.
        var scrimColor = Theme.Scrim.Resolve(ThemeMode.Light);
        var scrimIndex = Array.FindIndex(commands, c =>
            c.Kind == DrawCommandKind.FillRRect && c.Paint.Color == scrimColor);
        scrimIndex.Should().BeGreaterThan(0);
        commands[scrimIndex].Shape.Rect.Should().Be(new Rect(0, 0, 360, 480));

        var pageSurface = Theme.Colors(Variant.Primary).Base.Resolve(ThemeMode.Light);
        var lastPageIndex = Array.FindLastIndex(commands, c => c.Paint.Color == pageSurface);
        scrimIndex.Should().BeGreaterThan(lastPageIndex, "overlays paint after the page (painter's order)");
    }

    [Fact]
    public void DialogCard_CentersAtMin480_WithGutters()
    {
        // Wide viewport: the card caps at 480 and centers.
        var wide = new DisplayListBuilder();
        PhotonRealizer.Realize(PageWithDialog(), 800, 600, Theme, ThemeMode.Light, wide);
        var card = wide.Build().Commands.ToArray()
            .First(c => c.Kind == DrawCommandKind.FillRRect
                        && c.Paint.Color == Theme.Surface.Resolve(ThemeMode.Light)
                        && c.Shape.Radii.TopLeft == Radius.Xl);
        card.Shape.Rect.Width.Should().Be(480);
        card.Shape.Rect.X.Should().Be((800 - 480) / 2f, "centered");

        // Narrow viewport: screen − 48 gutters.
        var narrow = new DisplayListBuilder();
        PhotonRealizer.Realize(PageWithDialog(), 360, 600, Theme, ThemeMode.Light, narrow);
        var narrowCard = narrow.Build().Commands.ToArray()
            .First(c => c.Kind == DrawCommandKind.FillRRect
                        && c.Paint.Color == Theme.Surface.Resolve(ThemeMode.Light)
                        && c.Shape.Radii.TopLeft == Radius.Xl);
        narrowCard.Shape.Rect.Width.Should().Be(360 - 2 * Space.S6);
    }

    [Fact]
    public void Scrim_BlocksThePage_AndSwallowsWhenNotDismissible()
    {
        var pageTapped = false;
        var host = new PhotonHost(PageWithDialog(pageTap: () => pageTapped = true),
            Theme, ThemeMode.Light, 360, 480);
        var frame = host.RenderFrame(new DisplayListBuilder());

        // Tap where the page button sits — the scrim is on top and NOT dismissible: swallowed.
        var pageButton = frame.HitRegions.First();
        host.Tap(pageButton.Bounds.X + 4, pageButton.Bounds.Y + 4);
        pageTapped.Should().BeFalse("the scrim captures and swallows — a button must resolve the dialog");
    }

    [Fact]
    public void DismissibleScrim_FiresOnDismiss_AndDialogActionsResolve()
    {
        var dismissed = false;
        var kept = false;
        var column = new Column(gap: 0) { Width = SizeValue.Fill };
        column.Add(new Dialog("Sign out?", "You can sign back in anytime.",
            [new DialogAction("Stay", () => kept = true, Variant.Ghost), new DialogAction("Sign out")],
            dismissible: true, onDismiss: () => dismissed = true));

        var host = new PhotonHost(column, Theme, ThemeMode.Light, 360, 480);
        host.RenderFrame(new DisplayListBuilder());

        host.Tap(4, 4); // far corner: scrim, outside the card
        dismissed.Should().BeTrue("dismissible arms the scrim with OnDismiss");

        var frame = host.RenderFrame(new DisplayListBuilder());
        // Regions register scrim → Stay → Sign out (paint order); Stay is the FIRST button (Ghost first).
        var stay = frame.HitRegions.First(r => r.Bounds.Width < 200);
        host.Tap(stay.Bounds.X + stay.Bounds.Width / 2, stay.Bounds.Y + stay.Bounds.Height / 2);
        kept.Should().BeTrue("actions resolve through their callbacks");
    }

    [Fact]
    public void Dialog_RejectsMoreThanTwoActions()
    {
        var act = () => new Dialog("T", "B",
        [
            new DialogAction("A"), new DialogAction("B"), new DialogAction("C"),
        ]);
        act.Should().Throw<ArgumentOutOfRangeException>(
            "a third action means an ActionSheet or a screen (spec C2)");
    }

    [Theory]
    [InlineData(ThemeMode.Light, "dialog-light")]
    [InlineData(ThemeMode.Dark, "dialog-dark")]
    public void Dialog_Golden(ThemeMode mode, string golden)
    {
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(360, 300);
        var host = new PhotonHost(PageWithDialog(), PhotonTheme.Instance, mode, 360, 300);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        backend.Render(builder.Build(), surface);

        GoldenImage.Match(surface, golden);
    }
}
