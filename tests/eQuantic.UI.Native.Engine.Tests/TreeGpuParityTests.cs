using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The seam between the two halves of the pixel net, closed.
/// <para>
/// The GPU backends are verified against the Reference at the DISPLAY LIST level — a hand-built scene
/// from <see cref="GoldenScenes"/> drawn by both and compared. The component tree is verified at the
/// pixel level only through the Reference, because goldens must be deterministic and run on machines
/// with no GPU. Both halves are strong; nothing joined them, so a command that only a real COMPONENT
/// emits — a text run, a per-corner radius from a theme's shape scale, a clipped card, a shadow — had
/// never reached Metal or Vulkan in any automated test.
/// </para>
/// <para>
/// This is that join: a real tree, realized by <see cref="PhotonHost"/> exactly as a shell realizes
/// it, then the resulting display list drawn by both backends and compared with the shared tolerance.
/// The shells present through the GPU (<c>PhotonWindow.cs</c>, <c>PhotonViewController.cs</c>,
/// <c>PhotonActivity.cs</c>); this test is what proves the tree survives that trip.
/// </para>
/// </summary>
public class TreeGpuParityTests : IClassFixture<MetalFixture>
{
    private const int Width = 390;
    private const int Height = 844;

    private readonly MetalFixture _metal;

    public TreeGpuParityTests(MetalFixture metal) => _metal = metal;

    public static TheoryData<string> Trees => new() { "cards", "controls" };

    [SkippableTheory]
    [MemberData(nameof(Trees))]
    public void AComponentTreeReachesTheGpuUnchanged(string name)
    {
        Skip.IfNot(_metal.IsSupported, "No Metal device on this host — GPU parity runs on Apple hardware only.");

        // ONE display list, realized the way a shell realizes it, drawn by both. Rendering the tree
        // twice would compare two realizations rather than two rasterizers.
        var displayList = Realize(name);

        using var reference = new ReferenceBackend();
        var referencePixels = Draw(reference, displayList);
        var metalPixels = Draw(_metal.Backend, displayList);

        GpuParity.AssertFuzzyEqual("Metal", "tree-" + name, Width, Height, referencePixels, metalPixels);
    }

    /// <summary>A tree through the real host — the same call the shells make every frame.</summary>
    private static DisplayList Realize(string name)
    {
        var host = new PhotonHost(Tree(name), PhotonTheme.Instance, ThemeMode.Light, Width, Height);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        return builder.Build();
    }

    private static byte[] Draw(IRenderBackend backend, DisplayList displayList)
    {
        using var surface = backend.CreateSurface(Width, Height);
        backend.Render(displayList, surface);
        var pixels = new byte[Width * Height * 4];
        surface.ReadPixelsSrgb(pixels);
        return pixels;
    }

    /// <summary>
    /// Trees of real COMPONENTS rather than shapes: what they emit is the thing the hand-built golden
    /// scenes cannot cover, because a scene author draws what they thought of and a component draws
    /// what its own styling decided.
    /// </summary>
    private static VisualNode Tree(string name)
    {
        var theme = PhotonTheme.Instance;

        if (name == "cards")
        {
            var page = new Column(gap: Space.S3) { Width = SizeValue.Fill };
            page.Add(new Text("Cards", TypeRole.Heading, theme.TextPrimary));

            for (var i = 0; i < 4; i++)
            {
                var card = new Column(gap: Space.S2);
                card.Add(new Text($"Card {i}", TypeRole.Title, theme.TextPrimary, maxLines: 1));
                card.Add(new Text("Body copy that wraps across two lines the way a real card's does.",
                    TypeRole.BodyM, theme.TextSecondary, maxLines: 2));
                page.Add(new Box(new BoxStyle
                {
                    Width = SizeValue.Fill,
                    Background = theme.Surface,
                    CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Large)),
                    BorderWidth = 1,
                    BorderColor = theme.Border,
                    Padding = EdgeInsets.All(Space.S3),
                }, card));
            }

            return new Box(new BoxStyle
            {
                Padding = EdgeInsets.All(Space.S4),
                Background = theme.Background,
            }, page);
        }

        var controls = new Column(gap: Space.S3) { Width = SizeValue.Fill };
        controls.Add(new Text("Controls", TypeRole.Heading, theme.TextPrimary));
        controls.Add(new Button("Primary", Variant.Primary));
        controls.Add(new Button("Outline", Variant.Outline, SizeVariant.Small));
        var badges = new Row(gap: Space.S2) { Cross = CrossAlign.Center };
        badges.Add(new Badge(3));
        badges.Add(new Badge(12, variant: Variant.Success));
        controls.Add(badges);

        return new Box(new BoxStyle
        {
            Padding = EdgeInsets.All(Space.S4),
            Background = theme.Background,
        }, controls);
    }
}
