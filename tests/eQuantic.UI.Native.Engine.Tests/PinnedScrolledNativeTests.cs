using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A pinned header's <c>ScrolledStyle</c> on Photon (#506). Nothing read it here, so a header that
/// frosts and darkens on the web when the page scrolls under it stayed as it was on desktop and
/// mobile. It draws now while the surface it pins to, the nearest <see cref="ScrollView"/>, has
/// scrolled past <see cref="Pinned.ScrolledThreshold"/>: every member of the diff, over
/// <see cref="Pinned.ScrolledBase"/>, whose border is the hairline along the header's bottom edge.
/// The web twin is the header's <c>data-eq-scrolled</c>, set from the same surface.
/// </summary>
public class PinnedScrolledNativeTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;
    private static readonly ColorToken Veil = new(Color.FromRgb(0x10, 0x20, 0x30));
    private static readonly ColorToken Hairline = new(Color.FromRgb(0x40, 0x50, 0x60));

    private static readonly StyleDiff Frosted = new() { Background = Veil, BorderWidth = 1, BorderColor = Hairline };

    private static Pinned Header(StyleDiff? scrolled = null, TransitionSpec? transition = null) =>
        new(new Box(new BoxStyle { Width = 200, Height = 40 }))
        {
            ScrolledStyle = scrolled ?? Frosted,
            Transition = transition,
        };

    /// <summary>A 200dp window onto a header and a long page, scrolled to <paramref name="offset"/>.</summary>
    private static ScrollView Page(VisualNode header, float offset) =>
        new(new Column(gap: 0)
        {
            header,
            new Box(new BoxStyle { Width = 200, Height = 1000 }),
        })
        {
            Height = 200,
            Offset = offset,
        };

    private static DrawCommand[] Frame(VisualNode root, float timeMs = 0) =>
        Frame(new PhotonHost(root, Theme, ThemeMode.Light, 200, 200), timeMs);

    private static DrawCommand[] Frame(PhotonHost host, float timeMs = 0)
    {
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, timeMs);
        return builder.Build().Commands.ToArray();
    }

    private static bool Fills(DrawCommand[] frame, ColorToken token) =>
        frame.Any(c => c.Kind == DrawCommandKind.FillRRect && c.Paint.Color == token.Light);

    [Fact]
    public void AHeaderAtTheTopOfItsSurface_DrawsNothing()
    {
        Fills(Frame(Page(Header(), offset: 0)), Veil).Should().BeFalse();
    }

    [Fact]
    public void PastTheThreshold_TheHeaderDrawsItsVeilAndTheHairlineAlongItsBottom()
    {
        var frame = Frame(Page(Header(), offset: 20));

        Fills(frame, Veil).Should().BeTrue("the surface under the header has scrolled");
        var hairline = frame.Single(c => c.Kind == DrawCommandKind.StrokeRRect && c.Paint.Color == Hairline.Light);
        hairline.Clip.Should().NotBeNull("one edge of the outline is drawn, clipped to its band");
        var band = hairline.Clip!.Value.Rect;
        band.Height.Should().Be(1);
        band.Y.Should().Be(39, "the bottom edge of the 40dp header pinned at the top of the window");
    }

    [Theory]
    [InlineData(8, false)]
    [InlineData(9, true)]
    public void TheThreshold_IsTheWebs(float offset, bool scrolled)
    {
        Fills(Frame(Page(Header(), offset)), Veil).Should().Be(scrolled,
            "past 8dp, as the runtime marks a header past 8px");
    }

    /// <summary>Every member a diff can set, the one a developer reaches for first included: Flutter's
    /// <c>scrolledUnderElevation</c> is this <c>Elevation</c>, and the web ignored it.</summary>
    [Fact]
    public void EveryMemberOfTheDiff_Draws()
    {
        var glow = new ShadowSpec(0, 12, 0, Hairline);
        var frame = Frame(Page(Header(new StyleDiff
        {
            Background = Veil,
            Elevation = 2,
            Opacity = 0.5f,
            BackdropBlur = 12,
            Transform = Transform2D.Scale(0.5f),
            Shadows = [glow],
        }), offset: 20));

        frame.Should().Contain(c => c.Kind == DrawCommandKind.BeginLayer && c.StrokeWidth == 0.5f, "the opacity");
        frame.Should().Contain(c => c.Kind == DrawCommandKind.BackdropBlur, "the blur");
        frame.Count(c => c.Kind == DrawCommandKind.ShadowRRect).Should().Be(2, "the elevation's shadow and the glow");
        frame.Single(c => c.Kind == DrawCommandKind.FillRRect && c.Paint.Color == Veil.Light)
            .Transform.M11.Should().BeApproximately(0.5f, 1e-5f, "the transform");
    }

    [Fact]
    public void AHeaderOutsideEveryScrollView_IsNeverScrolled()
    {
        // The page does not scroll on Photon; a scroll view BESIDE the header is not its surface.
        var root = new Column(gap: 0)
        {
            Header(),
            new ScrollView(new Box(new BoxStyle { Width = 200, Height = 1000 }))
            {
                Height = 150,
                Offset = 300,
            },
        };

        Fills(Frame(root), Veil).Should().BeFalse();
    }

    [Fact]
    public void TheNearestScrollView_Decides()
    {
        // A panel that has not scrolled, inside a page that has: the header pins to the panel.
        var panel = new ScrollView(new Column(gap: 0)
        {
            Header(),
            new Box(new BoxStyle { Width = 200, Height = 600 }),
        })
        {
            Height = 120,
            Offset = 0,
        };
        Fills(Frame(Page(panel, offset: 50)), Veil).Should().BeFalse("its own surface is at the top");

        var scrolledPanel = new ScrollView(new Column(gap: 0)
        {
            Header(),
            new Box(new BoxStyle { Width = 200, Height = 600 }),
        })
        {
            Height = 120,
            Offset = 50,
        };
        Fills(Frame(Page(scrolledPanel, offset: 0)), Veil).Should().BeTrue("its own surface has scrolled");
    }

    /// <summary>A page whose scroll a test moves between frames, the shape a reader's scroll has.</summary>
    private sealed class Scrolling : StatefulComponent
    {
        public float Offset;
        public void ScrollTo(float offset) => SetState(() => Offset = offset);

        public override VisualNode Build(ComponentContext context) =>
            Page(Header(transition: TransitionSpec.Colors(100)), Offset);
    }

    /// <summary>Under its Transition the veil glides IN and OUT, as the web's background-color glides
    /// to its initial transparent when the rule stops applying. The header's is the frame's only fill,
    /// and a fill with nothing left of its alpha is not drawn.</summary>
    [Fact]
    public void UnderATransition_TheVeilGlidesInAndOut()
    {
        static Color? Fill(DrawCommand[] frame) =>
            frame.Where(c => c.Kind == DrawCommandKind.FillRRect).Select(c => (Color?)c.Paint.Color).SingleOrDefault();

        var page = new Scrolling();
        var host = new PhotonHost(page, Theme, ThemeMode.Light, 200, 200);
        Fill(Frame(host, 0)).Should().BeNull("at the top the header draws nothing");

        page.ScrollTo(20);
        Frame(host, 1000);
        Fill(Frame(host, 1050))!.Value.A.Should().BeInRange(1, 254, "halfway into the glide the veil is part-way in");
        Fill(Frame(host, 1200)).Should().Be(Veil.Light, "settled");

        page.ScrollTo(0);
        Frame(host, 2000);
        var leaving = Fill(Frame(host, 2050))!.Value;
        leaving.A.Should().BeInRange(1, 254, "and it fades out rather than vanishing");
        (leaving.R, leaving.G, leaving.B).Should().Be((Veil.Light.R, Veil.Light.G, Veil.Light.B),
            "keeping its hue, as the browser's premultiplied fade does, rather than darkening toward black");
        Fill(Frame(host, 2200)).Should().BeNull("gone once the glide ends");
    }
}
