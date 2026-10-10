using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A size that follows the window (#652) lowers to what the handoff wrote: <c>clamp(min, Nvw, max)</c>
/// and a unitless line height, since a line box in px stays put while the glyphs grow. The TS twin
/// writes the same strings (<c>fluid-type.spec.ts</c>).
/// </summary>
public class FluidTypeRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static string StyleOf(Text text) =>
        WebRealizer.Lower(text, Theme).Render().Attributes.GetValueOrDefault("style") ?? "";

    [Fact]
    public void AFluidSize_IsAClamp_WithTheLineBoxAsARatio()
    {
        var text = new Text("Fala com Portugal", TypeRole.BodyM)
        {
            StyleOverride = TypeStyle.OfSize(40, FontWeight.Bold).WithFluidSize(34, 4.2f, 54),
        };

        var style = StyleOf(text);

        style.Should().Contain("font-size: clamp(34px, 4.2vw, 54px)");
        style.Should().Contain("line-height: 1.25");
    }

    /// <summary>The handoff's -0.035em: tracking in px would stay put while the glyphs shrink.</summary>
    [Fact]
    public void TheTracking_FollowsAFluidSize_InEm()
    {
        var text = new Text("Fala", TypeRole.BodyM)
        {
            StyleOverride = new TypeStyle(54, 53, FontWeight.ExtraBold, -1.89f, 1.3f).WithFluidSize(34, 4.2f, 54),
        };

        StyleOf(text).Should().Contain("letter-spacing: -0.035em");
    }

    [Fact]
    public void ASizeInDp_IsUnchanged()
    {
        var text = new Text("Fala", TypeRole.BodyM) { StyleOverride = TypeStyle.OfSize(16, FontWeight.Regular) };

        var style = StyleOf(text);

        style.Should().Contain("font-size: 16px");
        style.Should().Contain("line-height: 20px");
        style.Should().NotContain("clamp");
    }

    [Fact]
    public void ATypeRoleThatFollowsTheWindow_IsAClampInTheSheet()
    {
        var css = PhotonCssGenerator.Generate(new NamedFaceTests.RoleTheme(Theme, TypeRole.Display,
            Theme.Type(TypeRole.Display).WithFluidSize(34, 4.2f, 54)));

        css.Should().Contain("font-size: clamp(34px, 4.2vw, 54px);");
    }
}
