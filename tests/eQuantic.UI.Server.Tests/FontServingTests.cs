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

    /// <summary>A family built by hand over a file this assembly embeds, which no theme names (#1056).</summary>
    private static readonly FontFamily HandBuilt = new("Probe Sans",
        new FontFile(Assembly, "eQuantic.UI.Fonts/TestFace-Regular.ttf", 400, 400));

    /// <summary>A family built by hand over a font no eQuantic build embedded as a face, which the theme does not name.</summary>
    private static readonly FontFamily Elsewhere = new("Elsewhere Sans",
        new FontFile(Assembly, "Elsewhere/TestFace-Elsewhere.ttf", 400, 400));

    [Page("/elsewhere-face-page")]
    public sealed class ElsewhereFacePage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            new Text("Handgloves", TypeRole.BodyM) { StyleOverride = context.Theme.Type(TypeRole.BodyM) with { Family = Elsewhere } };
    }

    /// <summary>A page whose text names the hand-built family twice, and the theme's own text face once.</summary>
    [Page("/named-face-page")]
    public sealed class NamedFacePage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var page = new Column();
            page.Add(new Text("Handgloves", TypeRole.BodyM) { StyleOverride = context.Theme.Type(TypeRole.BodyM) with { Family = HandBuilt } });
            page.Add(new Text("Again", TypeRole.BodyM) { StyleOverride = context.Theme.Type(TypeRole.BodyM) with { Family = HandBuilt } });
            page.Add(new Text("The theme's", TypeRole.BodyM) { StyleOverride = context.Theme.Type(TypeRole.BodyM) with { Family = TestFace } });
            return page;
        }
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
        var (regular, italic) = (served.Fonts.Single(font => font.Resource.EndsWith("Regular.ttf")), served.Fonts.Single(font => font.Resource.EndsWith("Italic.ttf")));
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
            (await response.Content.ReadAsByteArrayAsync()).Should().Equal(Resource(font.Resource));
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
        decompressed.ToArray().Should().Equal(Resource(font.Resource));
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

    /// <summary>
    /// A family a page names that the theme does not (#1056) is declared in that page's head, once however
    /// often the page names it, and its file is served at its content-addressed URL. The theme's own face,
    /// named at a call site, is left to the theme's stylesheet.
    /// </summary>
    [Fact]
    public async Task AFamilyAPageNames_IsDeclaredInItsHeadOnce_AndServed()
    {
        await using var app = await Serve(new FacedTheme(PhotonTheme.Instance, TestFace));
        var client = app.GetTestClient();
        var page = await client.GetStringAsync("/named-face-page");
        var head = page[..page.IndexOf("</head>", StringComparison.Ordinal)];

        var faces = Element(head, "eq-faces");
        faces.Should().Contain("font-family: \"Probe Sans\"").And.NotContain("eQuantic Test Face", "the theme's stylesheet declares it");
        System.Text.RegularExpressions.Regex.Matches(faces, "@font-face").Should().HaveCount(1, "one file, named twice");
        var url = System.Text.RegularExpressions.Regex.Match(faces, "url\\(\"(?<url>[^\"]+)\"\\)").Groups["url"].Value;
        url.Should().MatchRegex("^/_equantic/fonts/[0-9a-f]{16}\\.ttf$");
        (await client.GetByteArrayAsync(url)).Should().Equal(Resource("eQuantic.UI.Fonts/TestFace-Regular.ttf"));
    }

    /// <summary>
    /// What a page declares is served by every instance of the app, not only the one that rendered it: a
    /// second instance, which never rendered the page, answers its URL and the identity a browser asks
    /// for (Copilot's review of #1077). The set is the app's deployment, never the requests it served.
    /// </summary>
    [Fact]
    public async Task APageOneInstanceRendered_IsServedByAnotherThatNeverRenderedIt()
    {
        // Photon's theme, which ships no face: the file is the page's alone, and no stylesheet serves it.
        await using var rendering = await Serve(PhotonTheme.Instance);
        var page = await rendering.GetTestClient().GetStringAsync("/named-face-page");
        var url = System.Text.RegularExpressions.Regex.Match(Element(page, "eq-faces"), "url\\(\"(?<url>[^\"]+)\"\\)").Groups["url"].Value;
        url.Should().NotBeEmpty();

        await using var fresh = await Serve(PhotonTheme.Instance);
        var client = fresh.GetTestClient();

        (await client.GetByteArrayAsync(url)).Should().Equal(Resource("eQuantic.UI.Fonts/TestFace-Regular.ttf"));
        var identity = await client.GetAsync($"/_equantic/fonts/{Assembly}/eQuantic.UI.Fonts/TestFace-Regular.ttf");
        identity.Headers.Location!.OriginalString.Should().Be(url);
    }

    /// <summary>A family built by hand over a font no eQuantic build embedded as a face, and the theme does
    /// not name, is declared on no page: no instance but the one that read it could serve its URL.</summary>
    [Fact]
    public async Task AFamilyOverAFontNoBuildEmbeddedAsAFace_IsNotDeclared()
    {
        await using var app = await Serve(PhotonTheme.Instance);

        (await app.GetTestClient().GetStringAsync("/elsewhere-face-page")).Should().NotContain("Elsewhere Sans\"; src");
    }

    [Fact]
    public async Task APageThatNamesNoFamilyOutsideTheTheme_DeclaresNoFaceOfItsOwn()
    {
        await using var app = await Serve(new FacedTheme(PhotonTheme.Instance, TestFace));

        (await app.GetTestClient().GetStringAsync("/font-page")).Should().NotContain("id=\"eq-faces\"");
    }

    /// <summary>
    /// A page the browser builds asks for a face's file by its identity, since it does not know the hash
    /// of its bytes, and is sent on to the content-addressed URL (#1056). Only a face an eQuantic build
    /// embedded, in an assembly the app has loaded: anything else is a file the app never chose to publish.
    /// </summary>
    [Fact]
    public async Task AFaceAskedForByItsIdentity_IsSentToItsContentAddressedUrl()
    {
        await using var app = await Serve(PhotonTheme.Instance);
        var client = app.GetTestClient();

        var response = await client.GetAsync($"/_equantic/fonts/{Assembly}/eQuantic.UI.Fonts/TestFace-Italic.ttf");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.CacheControl!.NoCache.Should().BeTrue("a new build's file is found under its new hash");
        var location = response.Headers.Location!.OriginalString;
        location.Should().MatchRegex("^/_equantic/fonts/[0-9a-f]{16}\\.ttf$");
        (await client.GetByteArrayAsync(location)).Should().Equal(Resource("eQuantic.UI.Fonts/TestFace-Italic.ttf"));
    }

    /// <summary>
    /// A file the theme's stylesheet serves may be named by a family of a page's own, and a page the browser
    /// builds asks for it by its identity like any other, wherever the theme's author embedded it: it is
    /// served on a page the server renders, so it is on one the browser builds (Copilot's second review of
    /// #1077).
    /// </summary>
    [Fact]
    public async Task AFileTheThemeServes_IsFoundByItsIdentity_WhereverItIsEmbedded()
    {
        var themeFace = new FontFamily("Elsewhere Theme", new FontFile(Assembly, "Elsewhere/TestFace-Elsewhere.ttf", 400, 400));
        await using var app = await Serve(new FacedTheme(PhotonTheme.Instance, themeFace));
        var client = app.GetTestClient();

        var response = await client.GetAsync($"/_equantic/fonts/{Assembly}/Elsewhere/TestFace-Elsewhere.ttf");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await client.GetByteArrayAsync(response.Headers.Location!.OriginalString))
            .Should().Equal(Resource("Elsewhere/TestFace-Elsewhere.ttf"));
    }

    [Theory]
    [InlineData("Not.A.Loaded.Assembly/eQuantic.UI.Fonts/TestFace-Regular.ttf")]
    [InlineData(Assembly + "/eQuantic.UI.Fonts/Nowhere.ttf")]
    [InlineData(Assembly + "/eQuantic.UI.Fonts/TestFace-Regular.txt")]
    [InlineData("System.Private.CoreLib/FxResources.System.Private.CoreLib.SR.resources")]
    public async Task WhatNoEQuanticBuildEmbeddedAsAFace_IsNotFound(string identity)
    {
        await using var app = await Serve(PhotonTheme.Instance);

        var response = await app.GetTestClient().GetAsync("/_equantic/fonts/" + identity);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>The prefix the identity route takes is the one both SDKs embed a face under: written in
    /// two places, so this holds them together.</summary>
    [Theory]
    [InlineData("eQuantic.UI.Sdk")]
    [InlineData("eQuantic.UI.Sdk.Native")]
    public void TheIdentityRoutesPrefix_IsTheOneEachSdkEmbedsAFaceUnder(string sdk)
    {
        File.ReadAllText(Path.Combine(Repository.Root(), "src", sdk, "Sdk", "Sdk.targets"))
            .Should().Contain($"EQuanticFont=\"true\"")
            .And.Contain($"LogicalName=\"{ServedFonts.EmbeddedPrefix}%(Filename)%(Extension)\"");
    }

    /// <summary>The style element of <paramref name="head"/> whose id is <paramref name="id"/>, or the empty string.</summary>
    private static string Element(string head, string id)
    {
        var start = head.IndexOf($"id=\"{id}\"", StringComparison.Ordinal);
        return start < 0 ? "" : head[start..head.IndexOf("</style>", start, StringComparison.Ordinal)];
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
