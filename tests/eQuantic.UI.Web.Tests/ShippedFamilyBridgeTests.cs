using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using eQuantic.UI.Web.Build;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A family the theme ships crosses to the browser with its files (Copilot's third review of #1062):
/// the theme bridge sent its name alone, so a component reading <c>TextFamily.Files</c> saw two files
/// on the server and none after hydration, and the family no longer equalled the catalog's. The
/// generated theme, the client's other producer of one, writes them the same way.
/// </summary>
public class ShippedFamilyBridgeTests
{
    private static readonly FontFamily Brand = new("Brand Sans",
        new FontFile("Acme.App", "eQuantic.UI.Fonts/Brand.ttf", 200, 800),
        new FontFile("Acme.App", "eQuantic.UI.Fonts/Brand-Italic.ttf", 200, 800, Italic: true));

    private static readonly FontFamily BrandMono = new("Brand Mono",
        new FontFile("Acme.App", "eQuantic.UI.Fonts/BrandMono.ttf", 400, 400));

    [Fact]
    public void AShippedFamily_CrossesTheBridgeWithItsFiles_ForTheTextAndCodeFacesAndARole()
    {
        var json = ThemeBridge.SerializeJson(new ShippedTheme(PhotonTheme.Instance, Brand, BrandMono));

        const string brand = "[\"Brand Sans\",[\"Acme.App\",\"eQuantic.UI.Fonts/Brand.ttf\",200,800,false],"
            + "[\"Acme.App\",\"eQuantic.UI.Fonts/Brand-Italic.ttf\",200,800,true]]";
        json.Should().Contain("\"textFamily\":" + brand)
            .And.Contain("\"monoFamily\":[\"Brand Mono\",[\"Acme.App\",\"eQuantic.UI.Fonts/BrandMono.ttf\",400,400,false]]")
            .And.Contain(",false,false," + brand + "]", "the heading role names the family too");
    }

    /// <summary>A family with no files sends the payload a theme of platform faces always sent.</summary>
    [Fact]
    public void AFamilyWithNoFiles_CrossesAsItsName()
    {
        ThemeBridge.SerializeJson(new ShippedTheme(PhotonTheme.Instance, new FontFamily("Georgia"), null))
            .Should().Contain("\"textFamily\":\"Georgia\"").And.NotContain("monoFamily");
    }

    /// <summary>
    /// A file's names are text no rule vets, and the payload is written into a <c>&lt;script&gt;</c>
    /// element, where a JSON string does not stop the HTML parser: a <c>&lt;</c> is written as its
    /// escape, which the browser reads back as the character.
    /// </summary>
    [Fact]
    public void AFileNameThatWouldCloseTheScriptElement_IsWrittenAsItsEscape()
    {
        var hostile = new FontFamily("Brand Sans", new FontFile("Acme.App", "x</script><script>alert(1)</script>.ttf", 400, 400));

        var json = ThemeBridge.SerializeJson(new ShippedTheme(PhotonTheme.Instance, hostile, null));

        var escape = "\\" + "u003c";
        json.Should().NotContain("<")
            .And.Contain("\"x" + escape + "/script>" + escape + "script>alert(1)" + escape + "/script>.ttf\"");
    }

    [Fact]
    public void TheGeneratedTheme_WritesAShippedFamilysFiles_AsTheBridgeSendsThem()
    {
        var ts = DesignSystemTsGenerator.Generate(new ShippedTheme(PhotonTheme.Instance, Brand, BrandMono));

        ts.Should().Contain("import { ColorToken, FontFamily, FontFile, TypeStyle, VariantColors, codeTokenColor } from './value-types';")
            .And.Contain("textFamily: new FontFamily('Brand Sans', new FontFile(\"Acme.App\", \"eQuantic.UI.Fonts/Brand.ttf\", 200, 800, false), "
                + "new FontFile(\"Acme.App\", \"eQuantic.UI.Fonts/Brand-Italic.ttf\", 200, 800, true)),")
            .And.Contain("monoFamily: new FontFamily('Brand Mono', new FontFile(\"Acme.App\", \"eQuantic.UI.Fonts/BrandMono.ttf\", 400, 400, false)),");
    }

    /// <summary>Photon's theme with a text face, a code face and a heading set in the text face.</summary>
    internal sealed class ShippedTheme(IAppTheme inner, FontFamily text, FontFamily? mono) : IAppTheme
    {
        public FontFamily? TextFamily => text;
        public FontFamily? MonoFamily => mono;
        public ColorToken Background => inner.Background;
        public ColorToken Surface => inner.Surface;
        public ColorToken SurfaceSubtle => inner.SurfaceSubtle;
        public ColorToken SurfaceHighlight => inner.SurfaceHighlight;
        public ColorToken Border => inner.Border;
        public ColorToken BorderStrong => inner.BorderStrong;
        public ColorToken TextPrimary => inner.TextPrimary;
        public ColorToken TextSecondary => inner.TextSecondary;
        public ColorToken TextMuted => inner.TextMuted;
        public ColorToken TextInverse => inner.TextInverse;
        public ColorToken FocusRing => inner.FocusRing;
        public ColorToken LinkColor => inner.LinkColor;
        public ColorToken Scrim => inner.Scrim;
        public float DisabledOpacity => inner.DisabledOpacity;
        public VariantColors Colors(Variant variant) => inner.Colors(variant);
        public TypeStyle Type(TypeRole role) => role == TypeRole.Heading ? inner.Type(role) with { Family = text } : inner.Type(role);
        public ShadowSpec Elevation(int level) => inner.Elevation(level);
        public float Shape(ShapeScale scale) => inner.Shape(scale);
    }
}
