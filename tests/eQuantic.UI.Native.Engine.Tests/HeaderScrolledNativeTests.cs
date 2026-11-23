using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A box's <c>Scrolled</c> diff on Photon (#1769): the boxes INSIDE a pinned header follow its
/// scrolled state, which is the nearest <see cref="ScrollView"/>'s past
/// <see cref="Pinned.ScrolledThreshold"/>, so a header swaps what it shows as the page scrolls with
/// nothing rebuilt. The header's visit carries the state to its subtree, a nested header setting its
/// own, and the box lays the diff under every state of its own and its control's. The web twin is the
/// header-scrolled family, selecting on the header's <c>data-eq-scrolled</c>.
/// </summary>
public class HeaderScrolledNativeTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;
    private static readonly ColorToken Resting = new(Color.FromRgb(0x10, 0x20, 0x30));
    private static readonly ColorToken Dark = new(Color.FromRgb(0x40, 0x50, 0x60));
    private static readonly ColorToken Lit = new(Color.FromRgb(0x70, 0x80, 0x90));

    /// <summary>A link in a transparent header: light at rest, dark once the header is scrolled.</summary>
    private static Box Link(StyleDiff? hover = null) =>
        new(new BoxStyle
        {
            Width = 80, Height = 32,
            Background = Resting,
            Scrolled = new StyleDiff { Background = Dark },
            Hover = hover,
        });

    /// <summary>A 200dp window onto <paramref name="top"/> and a long page, scrolled to <paramref name="offset"/>.</summary>
    private static ScrollView Page(VisualNode top, float offset) =>
        new(new Column(gap: 0)
        {
            top,
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

    [Theory]
    [InlineData(0, false)]
    [InlineData(8, false)]
    [InlineData(9, true)]
    [InlineData(300, true)]
    public void ABoxInAHeader_ShowsItsDiffWhileTheHeaderIsScrolled(float offset, bool scrolled)
    {
        var frame = Frame(Page(new Pinned(Link()), offset));

        Fills(frame, Dark).Should().Be(scrolled, "past 8dp the header is scrolled, as the web marks it past 8px");
        Fills(frame, Resting).Should().Be(!scrolled);
    }

    /// <summary>The header styles nothing of its own: its boxes follow its scroll all the same.</summary>
    [Fact]
    public void AHeaderWithNoScrolledStyle_StillCarriesItsState()
    {
        new Pinned(Link()).ScrolledStyle.Should().BeNull();

        Fills(Frame(Page(new Pinned(Link()), 20)), Dark).Should().BeTrue();
    }

    [Fact]
    public void ABoxInNoHeader_KeepsItsRestingStyle()
    {
        var frame = Frame(Page(Link(), 300));

        Fills(frame, Dark).Should().BeFalse("no header is around it, however far the page has scrolled");
        Fills(frame, Resting).Should().BeTrue();
    }

    /// <summary>The badge's layer: faded and shrunk once the header is scrolled, every member of the diff.</summary>
    [Fact]
    public void TheBadgeFadesAndShrinks()
    {
        var badge = new Box(new BoxStyle
        {
            Width = 96, Height = 96,
            Background = Resting,
            Scrolled = new StyleDiff { Opacity = 0.25f, Transform = Transform2D.Scale(0.5f) },
        });

        var rest = Frame(Page(new Pinned(badge), 0));
        rest.Should().NotContain(c => c.Kind == DrawCommandKind.BeginLayer, "at rest the badge is opaque");

        var frame = Frame(Page(new Pinned(badge), 20));
        frame.Should().Contain(c => c.Kind == DrawCommandKind.BeginLayer && c.StrokeWidth == 0.25f, "the opacity");
        frame.Single(c => c.Kind == DrawCommandKind.FillRRect && c.Paint.Color == Resting.Light)
            .Transform.M11.Should().BeApproximately(0.5f, 1e-5f, "the transform");
    }

    /// <summary>
    /// The NEAREST header decides, as the nearest control decides a focus and a press: a pinned rail
    /// whose own scroller pins its group headers. Carried as "any header around it is scrolled", the
    /// rail's scroll lit every group's links while their own list sat at its top.
    /// </summary>
    [Fact]
    public void TheNearestHeader_Decides()
    {
        static VisualNode Rail(float listOffset) => new Pinned(new ScrollView(new Column(gap: 0)
        {
            new Pinned(Link()),
            new Box(new BoxStyle { Width = 200, Height = 600 }),
        })
        {
            Height = 120,
            Offset = listOffset,
        });

        Fills(Frame(Page(Rail(listOffset: 0), offset: 50)), Dark).Should().BeFalse(
            "the rail is scrolled, and the group's own header, at the top of its list, is not");
        Fills(Frame(Page(Rail(listOffset: 50), offset: 0)), Dark).Should().BeTrue(
            "the group's header is scrolled, under a rail that is not");
    }

    /// <summary>A link in a scrolled header still lights under the pointer: the hover is laid over the
    /// scrolled diff, as the web's hover outweighs the header-scrolled family.</summary>
    [Fact]
    public void AHover_BeatsTheScrolledDiff()
    {
        var hovered = new Simulated(SimulatedState.Hovered, Link(hover: new StyleDiff { Background = Lit }));

        var frame = Frame(Page(new Pinned(hovered), 20));

        Fills(frame, Lit).Should().BeTrue("the hover's fill");
        Fills(frame, Dark).Should().BeFalse("not the scrolled one under it");
    }

    /// <summary>
    /// A panel the header opens is a layer of its own on Photon, drawn after the page with a scope of
    /// its own, and on the web an element inside the header's. Its boxes follow the header there, and
    /// here as well: the layer carries the state it was found in.
    /// </summary>
    [Fact]
    public void APanelTheHeaderOpens_FollowsItsScroll()
    {
        static VisualNode Menu() => new Pinned(new Anchored(
            new Box(new BoxStyle { Width = 80, Height = 32 }),
            Link())
        {
            Open = true,
        });

        Fills(Frame(Page(Menu(), 20)), Dark).Should().BeTrue("the header it was opened from is scrolled");
        Fills(Frame(Page(Menu(), 0)), Dark).Should().BeFalse("and at rest it rests");
    }

    /// <summary>A page whose scroll a test moves between frames, the shape a reader's scroll has.</summary>
    private sealed class Scrolling : StatefulComponent
    {
        public float Offset;
        public void ScrollTo(float offset) => SetState(() => Offset = offset);

        public override VisualNode Build(ComponentContext context) =>
            Page(new Pinned(new Box(new BoxStyle
            {
                Width = 96, Height = 96,
                Background = Resting,
                Transition = new TransitionSpec(StyleChannels.Opacity, 100),
                Scrolled = new StyleDiff { Opacity = 0 },
            })), Offset);
    }

    /// <summary>The box's own Transition glides the swap both ways, as it glides a hover.</summary>
    [Fact]
    public void UnderItsTransition_TheBoxGlidesOutAndBackIn()
    {
        static float Alpha(DrawCommand[] frame) =>
            frame.Where(c => c.Kind == DrawCommandKind.BeginLayer).Select(c => c.StrokeWidth).DefaultIfEmpty(1f).Single();

        var page = new Scrolling();
        var host = new PhotonHost(page, Theme, ThemeMode.Light, 200, 200);
        Alpha(Frame(host, 0)).Should().Be(1f, "at the top the badge is opaque");

        page.ScrollTo(20);
        Frame(host, 1000);
        Alpha(Frame(host, 1050)).Should().BeInRange(0.01f, 0.99f, "halfway out, part-way faded");

        page.ScrollTo(0);
        Frame(host, 2000);
        Alpha(Frame(host, 2050)).Should().BeInRange(0.01f, 0.99f, "and halfway back in");
        Alpha(Frame(host, 2200)).Should().Be(1f, "opaque again once the glide ends");
    }
}
