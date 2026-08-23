using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The one size that cannot be written as a number.
/// <para>
/// "As tall as the window allows, then scroll" is the requirement of every menu, dropdown, dialog
/// and sheet, and no constant expresses it: reported from a real page whose panel is 550dp, a cap
/// of 620 leaves 215 unused in a 900px window and still overflows in a 700px one. The vocabulary
/// says what it wants — the window, less this — and each realizer answers with the window it has.
/// </para>
/// <para>
/// Deliberately NOT a viewport unit. `vh` is the web's word and means nothing to a Photon window;
/// this is the same shape as WindowSizeClass, which is the other axis.
/// </para>
/// </summary>
public class WindowRelativeSizeTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static string StyleOf(VisualNode node) =>
        WebRealizer.Lower(node, Theme).Render().Attributes.GetValueOrDefault("style", "");

    [Fact]
    public void ACapIsTheWindowLessTheInset()
    {
        var panel = new Box(new BoxStyle
        {
            MaxHeight = SizeValue.WindowMinus(88),
            Background = Theme.Surface,
        }, new Text("menu"));

        StyleOf(panel).Should().Contain("max-height: calc(100vh - 88px)");
    }

    [Fact]
    public void TheAxisDecidesTheUnit()
    {
        // The only kind for which the axis matters: the window is 100vh down and 100vw across.
        StyleOf(new Box(new BoxStyle { Width = SizeValue.WindowMinus(24) }, new Text("x")))
            .Should().Contain("width: calc(100vw - 24px)");
        StyleOf(new Box(new BoxStyle { Height = SizeValue.WindowMinus(24) }, new Text("x")))
            .Should().Contain("height: calc(100vh - 24px)");
    }

    [Fact]
    public void NoInsetIsTheWholeWindow()
    {
        StyleOf(new Box(new BoxStyle { MaxHeight = SizeValue.WindowMinus(0) }, new Text("x")))
            .Should().Contain("max-height: 100vh");
    }

    [Fact]
    public void APlainNumberStillMeansDp()
    {
        // The implicit conversion keeps every existing cap compiling and rendering unchanged —
        // this is why the type could change at all.
        StyleOf(new Box(new BoxStyle { MaxWidth = 980 }, new Text("x")))
            .Should().Contain("max-width: 980px");
    }

    [Fact]
    public void NoCapIsStillNoCap()
    {
        StyleOf(new Box(new BoxStyle { Background = Theme.Surface }, new Text("x")))
            .Should().NotContain("max-height").And.NotContain("max-width");
    }
}
