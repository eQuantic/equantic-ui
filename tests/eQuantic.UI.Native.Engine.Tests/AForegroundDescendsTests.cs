using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A foreground descends to the text and icons that name no colour (#614), Flutter's
/// <c>DefaultTextStyle</c> and <c>IconTheme</c>: a link or a row brightens its label and its arrow
/// under the pointer by stating the foreground once. The web half is
/// <c>AForegroundDescendsRealizerTests</c>.
/// <para>
/// With no platform text service, a text draws as bars in its ink at 30%, and an icon with no
/// rasterizer as a block in its tint at 30%: the colour is what these read.
/// </para>
/// </summary>
public class AForegroundDescendsTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;
    private static readonly ColorToken Quiet = new(Color.FromRgb(0x60, 0x60, 0x60));
    private static readonly ColorToken Bright = new(Color.FromRgb(0x10, 0x90, 0xF0));
    private static readonly ColorToken Own = new(Color.FromRgb(0xC0, 0x20, 0x20));

    private static bool Paints(DisplayList list, ColorToken token)
    {
        var color = token.Resolve(ThemeMode.Light).WithOpacity(0.30f);
        foreach (var command in list.Commands)
            if (command.Kind == DrawCommandKind.FillRRect && command.Paint.Color == color) return true;
        return false;
    }

    private static DisplayList Frame(PhotonHost host)
    {
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        return builder.Build();
    }

    private static DisplayList Draw(VisualNode node)
    {
        var builder = new DisplayListBuilder();
        PhotonRealizer.Realize(node, 400, 100, Theme, ThemeMode.Light, builder);
        return builder.Build();
    }

    private static Row Content(ColorToken? textColor = null)
    {
        var row = new Row(gap: 8);
        row.Add(new Text("Continue", TypeRole.Label, textColor));
        row.Add(new Icon(Icons.Search, IconSize.Sm));
        return row;
    }

    [Fact]
    public void ATextAndAnIconWithNoColour_TakeTheBoxsForeground()
    {
        var frame = Draw(new Box(new BoxStyle { Foreground = Quiet }, Content()));

        Paints(frame, Quiet).Should().BeTrue("the text and the icon are drawn in the box's foreground");
        Paints(frame, Theme.TextPrimary).Should().BeFalse("and not in TextPrimary");
    }

    [Fact]
    public void WithNoForegroundAbove_TheyAreTextPrimary() =>
        Paints(Draw(new Box(new BoxStyle(), Content())), Theme.TextPrimary).Should().BeTrue();

    [Fact]
    public void ATextThatNamesItsColour_KeepsIt()
    {
        var frame = Draw(new Box(new BoxStyle { Foreground = Quiet }, Content(Own)));

        Paints(frame, Own).Should().BeTrue("the text names its colour");
        Paints(frame, Quiet).Should().BeTrue("and the icon, which names none, takes the foreground");
    }

    [Fact]
    public void TheNearestBoxThatStatesOne_Decides() =>
        Paints(Draw(new Box(new BoxStyle { Foreground = Own },
            new Box(new BoxStyle { Foreground = Quiet }, Content()))), Quiet).Should().BeTrue();

    /// <summary>The scenario: a control's surface states a quiet foreground and a bright one on hover,
    /// and its label and arrow brighten together when the control is hovered.</summary>
    [Fact]
    public void AControlsHover_BrightensItsLabelAndArrow()
    {
        var control = new Pressable(new Box(new BoxStyle
        {
            Width = 300, Height = 40,
            Foreground = Quiet,
            Hover = new StyleDiff { Foreground = Bright },
        }, Content()), () => { });
        var host = new PhotonHost(control, Theme, ThemeMode.Light, 400, 100);

        var rest = Frame(host);
        Paints(rest, Quiet).Should().BeTrue("at rest the label and the arrow are quiet");

        host.PointerMove(20, 20);
        var hovered = Frame(host);
        Paints(hovered, Bright).Should().BeTrue("hovered, they brighten together");
        Paints(hovered, Quiet).Should().BeFalse("both of them");
    }

    /// <summary>A pinned header's scrolled diff hands its foreground down, as the web writes it as
    /// <c>color</c> under the scrolled marker.</summary>
    [Fact]
    public void APinnedHeadersScrolledForeground_Descends()
    {
        static ScrollView Page(float offset) => new(new Column(gap: 0)
        {
            new Pinned(new Box(new BoxStyle { Width = 200, Height = 40 }, new Text("Header", TypeRole.Label)))
            {
                ScrolledStyle = new StyleDiff { Background = Own, Foreground = Bright },
            },
            new Box(new BoxStyle { Width = 200, Height = 1000 }),
        })
        { Height = 200, Offset = offset };
        DisplayList At(float offset) => Frame(new PhotonHost(Page(offset), Theme, ThemeMode.Light, 200, 200));

        Paints(At(0), Bright).Should().BeFalse("at the top the header is not scrolled");
        Paints(At(20), Bright).Should().BeTrue("scrolled, its label takes the scrolled foreground");
    }
}
