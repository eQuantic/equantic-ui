using eQuantic.UI.Conformance.Tests.Infrastructure;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A chosen theme crosses to the browser as data, and the served runtime rebuilds it before hydration
/// (<c>ThemeBridge.SerializeJson</c>, <c>materializeTheme</c>): a family the theme ships is the same
/// family on both sides, its files included. Sent by its name alone, a component read two files on the
/// server and none in the browser (Copilot's third review of #1062).
/// </summary>
public class ShippedFamilyHydrationTests
{
    private static readonly FontFamily Brand = new("Brand Sans",
        new FontFile("Acme.App", "eQuantic.UI.Fonts/Brand.ttf", 200, 800),
        new FontFile("Acme.App", "eQuantic.UI.Fonts/Brand-Italic.ttf", 200, 800, Italic: true));

    private static readonly FontFamily BrandMono = new("Brand Mono",
        new FontFile("Acme.App", "eQuantic.UI.Fonts/BrandMono.ttf", 400, 400));

    [SkippableFact]
    public void AShippedFamily_IsTheSameFamilyAfterHydration()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        var theme = new ShippedTheme(PhotonTheme.Instance, Brand, BrandMono);
        var js = $$"""
            const theme = materializeTheme({{ThemeBridge.SerializeJson(theme)}});
            const describe = (family) => family
              ? [family.name, ...family.files.map((file) => [file.assembly, file.resource, file.minWeight, file.maxWeight, file.italic].join('/'))].join(',')
              : 'none';
            console.log([describe(theme.textFamily), describe(theme.monoFamily), describe(theme.type('heading').family)].join('|'));
            """;

        var answered = JsExecutor.Run(ConformanceRunner.ImportOfWhatItNames(js) + js).Trim();

        answered.Should().Be(string.Join('|', Describe(theme.TextFamily), Describe(theme.MonoFamily),
            Describe(theme.Type(TypeRole.Heading).Family)));
    }

    private static string Describe(FontFamily? family) => family is null
        ? "none"
        : string.Join(',', [family.Name, .. family.Files.Select(file =>
            $"{file.Assembly}/{file.Resource}/{file.MinWeight}/{file.MaxWeight}/{(file.Italic ? "true" : "false")}")]);

    /// <summary>Photon's theme with a text face, a code face and a heading set in the text face.</summary>
    private sealed class ShippedTheme(IAppTheme inner, FontFamily text, FontFamily mono) : IAppTheme
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
