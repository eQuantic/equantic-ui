using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using eQuantic.UI.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The faces a theme ships, as a served page and the font route answer them (#1055): every file
/// declared in the theme's stylesheet, the text face's upright files preloaded, and each one served
/// by the hash of its bytes, immutable. A theme that ships no face writes the head it wrote before.
/// </summary>
public class FontServingTests
{
    private const string Assembly = "eQuantic.UI.Server.Tests";

    private static readonly FontFamily TestFace = new("eQuantic Test Face",
        new FontFile(Assembly, "eQuantic.UI.Fonts/TestFace-Regular.ttf", 400, 400),
        new FontFile(Assembly, "eQuantic.UI.Fonts/TestFace-Italic.ttf", 400, 400, Italic: true));

    [Page("/font-page")]
    public sealed class FontPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("Handgloves", TypeRole.BodyM);
    }

    /// <summary>A catalog, as the SDK writes one for an app: public static families.</summary>
    public static class TestCatalog
    {
        public static FontFamily TestFace { get; } = FontServingTests.TestFace;
    }

    [Fact]
    public async Task AThemeThatShipsItsTextFace_DeclaresBothFiles_PreloadsTheUprightOne_AndServesEach()
    {
        await using var app = await Serve(new FacedTheme(PhotonTheme.Instance, TestFace));
        var client = app.GetTestClient();
        var page = await client.GetStringAsync("/font-page");
        var head = page[..page.IndexOf("</head>", StringComparison.Ordinal)];

        var served = ServedFonts.Of(new FacedTheme(PhotonTheme.Instance, TestFace), [], logger: null);
        served.Fonts.Should().HaveCount(2);
        var (regular, italic) = (served.Fonts.Single(font => !font.File.Italic), served.Fonts.Single(font => font.File.Italic));
        regular.Url.Should().MatchRegex("^/_equantic/fonts/[0-9a-f]{16}\\.ttf$");

        head.Should().Contain($"<link rel=\"preload\" href=\"{regular.Url}\" as=\"font\" type=\"font/ttf\" crossorigin>")
            .And.NotContain($"href=\"{italic.Url}\" as=\"font\"", "only the text face's upright files are preloaded");
        head.Should().Contain($"@font-face {{ font-family: \"eQuantic Test Face\"; src: url(\"{regular.Url}\") format(\"truetype\"); font-weight: 400; font-style: normal; font-display: swap; }}")
            .And.Contain($"@font-face {{ font-family: \"eQuantic Test Face\"; src: url(\"{italic.Url}\") format(\"truetype\"); font-weight: 400; font-style: italic; font-display: swap; }}");
        head.IndexOf("@font-face", StringComparison.Ordinal).Should()
            .BeLessThan(head.IndexOf("--eq-font-family", StringComparison.Ordinal), "the faces come before the sheet that names them");

        foreach (var font in served.Fonts)
        {
            var response = await client.GetAsync(font.Url);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.Should().Be("font/ttf");
            response.Headers.CacheControl!.ToString().Should().Be("public, max-age=31536000, immutable");
            (await response.Content.ReadAsByteArrayAsync()).Should().Equal(Resource(font.File.Resource));
        }
    }

    /// <summary>Served compressed to a browser that takes Brotli, as every current one does, and whole
    /// to one that does not: an OpenType file is served raw, and compresses to about half.</summary>
    [Fact]
    public async Task AFaceIsServedCompressed_ToABrowserThatTakesBrotli()
    {
        await using var app = await Serve(new FacedTheme(PhotonTheme.Instance, TestFace));
        var font = ServedFonts.Of(new FacedTheme(PhotonTheme.Instance, TestFace), [], logger: null).Fonts[0];
        var request = new HttpRequestMessage(HttpMethod.Get, font.Url);
        request.Headers.AcceptEncoding.ParseAdd("gzip, br");

        var response = await app.GetTestClient().SendAsync(request);

        response.Content.Headers.ContentEncoding.Should().Equal(["br"]);
        response.Headers.Vary.Should().Contain("Accept-Encoding");
        var compressed = await response.Content.ReadAsByteArrayAsync();
        compressed.Length.Should().BeLessThan(font.Bytes.Length);
        using var brotli = new System.IO.Compression.BrotliStream(new MemoryStream(compressed), System.IO.Compression.CompressionMode.Decompress);
        using var decompressed = new MemoryStream();
        brotli.CopyTo(decompressed);
        decompressed.ToArray().Should().Equal(Resource(font.File.Resource));
    }

    [Fact]
    public async Task AFontNothingNames_IsNotFound()
    {
        await using var app = await Serve(new FacedTheme(PhotonTheme.Instance, TestFace));

        var response = await app.GetTestClient().GetAsync("/_equantic/fonts/0123456789abcdef.ttf");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AThemeThatShipsNoFace_DeclaresNoneAndPreloadsNone()
    {
        await using var app = await Serve(PhotonTheme.Instance);
        var page = await app.GetTestClient().GetStringAsync("/font-page");

        page.Should().NotContain("@font-face").And.NotContain("as=\"font\"");
    }

    [Fact]
    public void ACatalogsFamily_IsDeclared_AndNotPreloaded()
    {
        var catalog = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("eQuantic.Fonts.Probe"), AssemblyBuilderAccess.Run);
        catalog.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(FontCatalogAttribute).GetConstructor([typeof(Type)])!, [typeof(TestCatalog)]));

        var served = ServedFonts.Of(PhotonTheme.Instance, [catalog], logger: null);

        served.Rules.Should().Contain("font-family: \"eQuantic Test Face\"", "a page may name the family at a call site");
        served.Preloads.Should().BeEmpty("only the theme's text face is preloaded");
    }

    /// <summary>A family missing one of its files is not served at all, as no shell draws one: its
    /// text falls back on the web as it does on Photon, rather than being set in the other cut.</summary>
    [Fact]
    public void AFamilyMissingOneOfItsFiles_IsNotServed()
    {
        var half = new FontFamily("eQuantic Test Face",
            new FontFile(Assembly, "eQuantic.UI.Fonts/TestFace-Regular.ttf", 400, 400),
            new FontFile(Assembly, "eQuantic.UI.Fonts/TestFace-Missing-Italic.ttf", 400, 400, Italic: true));

        var served = ServedFonts.Of(new FacedTheme(PhotonTheme.Instance, half), [], logger: null);

        served.Fonts.Should().BeEmpty();
        served.Rules.Should().BeEmpty();
        served.Preloads.Should().BeEmpty();
    }

    [Fact]
    public void AFileItsAssemblyDoesNotHold_IsNotServed()
    {
        var missing = new FontFamily("Nowhere Sans", new FontFile(Assembly, "eQuantic.UI.Fonts/Nowhere.ttf", 400, 400));

        ServedFonts.Of(new FacedTheme(PhotonTheme.Instance, missing), [], logger: null).Fonts.Should().BeEmpty();
    }

    private static async Task<WebApplication> Serve(IAppTheme theme)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options => options.ScanAssembly(System.Reflection.Assembly.GetExecutingAssembly()).UseTheme(theme));
        var app = builder.Build();
        app.MapUI();
        await app.StartAsync();
        return app;
    }

    private static byte[] Resource(string name)
    {
        using var stream = typeof(FontServingTests).Assembly.GetManifestResourceStream(name)!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>Photon's theme with a text face of its own.</summary>
    private sealed class FacedTheme(IAppTheme inner, FontFamily text) : IAppTheme
    {
        public FontFamily? TextFamily => text;
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
        public TypeStyle Type(TypeRole role) => inner.Type(role);
        public ShadowSpec Elevation(int level) => inner.Elevation(level);
        public float Shape(ShapeScale scale) => inner.Shape(scale);
    }
}
