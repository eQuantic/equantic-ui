using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A theme that brands its type names a FACE, and the web quotes it in front of the stack the page
/// would have used anyway. Reported by the eQuantic Code IDE: a design handoff draws every metric
/// against IBM Plex Sans and JetBrains Mono, the SDK rendered the system faces, and nothing said so
/// — typography being most of what a UI looks like, that was the largest gap between the design and
/// what could be built.
/// </summary>
public class NamedFaceTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static string? FamilyOf(Text text) =>
        WebRealizer.Lower(text, Theme).Render().Attributes.GetValueOrDefault("style") is { } style
        && style.Contains("font-family")
            ? style[(style.IndexOf("font-family", StringComparison.Ordinal))..]
            : null;

    [Fact]
    public void NoFaceNamed_LeavesTheStackAlone()
    {
        var text = new Text("hello", TypeRole.BodyM);

        FamilyOf(text).Should().BeNull("an app with no opinion inherits the document's own face");
    }

    [Fact]
    public void ANamedFace_IsQuotedInFrontOfTheStackThePageWouldHaveUsed()
    {
        var text = new Text("hello", TypeRole.BodyM)
        {
            StyleOverride = Theme.Type(TypeRole.BodyM) with { Family = "IBM Plex Sans" },
        };

        var family = FamilyOf(text);
        family.Should().NotBeNull();
        // Quoted, because the common case has a space in it and an unquoted family is a parse error
        // that takes the whole declaration with it.
        family.Should().Contain("\"IBM Plex Sans\"");
        family.Should().Contain("--eq-font-family", "an unavailable face lands on the document's stack");
    }

    [Fact]
    public void ANamedMonoFace_FallsBackToTheMonoStack()
    {
        var text = new Text("code", TypeRole.BodyM)
        {
            StyleOverride = Theme.Type(TypeRole.BodyM) with { Family = "JetBrains Mono", Mono = true },
        };

        var family = FamilyOf(text);
        family.Should().Contain("\"JetBrains Mono\"");
        family.Should().Contain("--eq-font-mono", "a code face that is missing must not land on a proportional one");
    }

    // ---- What may be spelled as a family ------------------------------------------------------

    /// <summary>
    /// The rule, against the SAME fixture the TypeScript spec reads. Two hand-written tables would
    /// drift, and the drift lands as a hydration mismatch: SSR emits <c>font-family</c> through this
    /// predicate and the client emits it through <c>isWellFormedFace</c>.
    /// </summary>
    [Fact]
    public void TheFamilyRule_IsTheSameRuleTheClientHolds()
    {
        var disagreed = FaceCases()
            .Where(c => FaceName.IsWellFormed(c.Family) != c.WellFormed)
            .Select(c => $"{Printable(c.Family)} (expected {c.WellFormed}: {c.Why})")
            .ToArray();

        disagreed.Should().BeEmpty("the fixture is the contract both sides answer");
    }

    /// <summary>
    /// The CSS injection. A family is the only free-form text in the style pipeline, and the role
    /// sheet writes it into a <c>&lt;style&gt;</c> element — where a CSS string does NOT neutralise
    /// <c>&lt;/style&gt;</c> for the HTML parser, so quoting was never the defence it looked like.
    /// </summary>
    [Fact]
    public void AFamilyThatWouldCloseTheStyleElement_NeverReachesTheSheet()
    {
        FaceResolution.Clear();
        var text = new Text("hello", TypeRole.BodyM)
        {
            StyleOverride = Theme.Type(TypeRole.BodyM) with { Family = "</style><script>alert(1)</script>" },
        };

        FamilyOf(text).Should().BeNull("a name no emitter will accept is not embedded");
        FaceResolution.Unresolved.Should().ContainSingle()
            .Which.Should().Contain("</style>", "and it is REPORTED rather than dropped in silence");
    }

    /// <summary>
    /// The JSON injection, which is the same defect in the other syntax: the theme payload is
    /// written verbatim into a <c>&lt;script&gt;</c> element, so a family containing
    /// <c>&lt;/script&gt;</c> ends the element rather than the string and no JSON quoting helps.
    /// </summary>
    [Fact]
    public void AFamilyThatWouldCloseTheScriptElement_NeverReachesTheWire()
    {
        FaceResolution.Clear();
        var json = ThemeBridge.SerializeJson(new FacedTheme(Theme, "</script>"));

        json.Should().NotContain("</script>", "the payload sits inside a script element");
        FaceResolution.Unresolved.Should().Contain("</script>");
    }

    [Fact]
    public void AWellFormedFamily_StillReachesTheWire()
    {
        FaceResolution.Clear();
        var json = ThemeBridge.SerializeJson(new FacedTheme(Theme, "IBM Plex Sans"));

        json.Should().Contain("IBM Plex Sans", "the rule rejects names, not brands");
        FaceResolution.Unresolved.Should().BeEmpty();
    }

    // ---- The released surface -------------------------------------------------------------------

    /// <summary>
    /// <c>TypeStyle</c> gained a parameter, and a positional record answers that by REPLACING both
    /// halves of its surface: the constructor and the synthesised <c>Deconstruct</c>. C# optional
    /// parameters are not overloads — the default is baked into each call site — so an assembly
    /// compiled against the released package calls signatures that no longer exist and finds a
    /// <c>MissingMethodException</c> at load.
    /// <para>
    /// Reflection rather than a call, because a call would be compiled against THIS tree and would
    /// pass by binding to the eight-parameter form. The question is what the metadata offers an
    /// assembly that was compiled elsewhere.
    /// </para>
    /// </summary>
    [Fact]
    public void TypeStyle_StillOffersTheSurfaceItShippedWith()
    {
        var constructor = typeof(TypeStyle).GetConstructors()
            .Where(c => c.GetParameters().Length == 7)
            .ToArray();
        constructor.Should().ContainSingle("a consumer compiled against the 7-parameter form calls it");

        var deconstruct = typeof(TypeStyle).GetMethods()
            .Where(m => m.Name == "Deconstruct" && m.GetParameters().Length == 7)
            .ToArray();
        deconstruct.Should().ContainSingle(
            "and `var (size, line, weight, tracking, scale, mono, italic) = style;` binds to it");

        // The half that is easy to lose: the same value, whichever way it is read.
        var style = new TypeStyle(15, 20, FontWeight.Regular, 0, 1.3f, false, false);
        var (size, line, weight, tracking, scale, mono, italic) = style;
        (size, line, weight, tracking, scale, mono, italic)
            .Should().Be((15f, 20f, FontWeight.Regular, 0f, 1.3f, false, false));
        style.Family.Should().BeNull("the compatibility shape has no face, which is the default");
    }

    // ---- fixture plumbing -----------------------------------------------------------------------

    private sealed record FaceCase(string Family, bool WellFormed, string Why);

    private static IEnumerable<FaceCase> FaceCases()
    {
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(FixturePath()));
        foreach (var element in document.RootElement.GetProperty("cases").EnumerateArray())
            yield return new FaceCase(
                element.GetProperty("family").GetString()!,
                element.GetProperty("wellFormed").GetBoolean(),
                element.GetProperty("why").GetString()!);
    }

    private static string FixturePath()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;

        here.Should().NotBeNull("the test has to find the repository root to read the fixture");
        return Path.Combine(here!.FullName, "src", "eQuantic.UI.Runtime", "src", "shared",
            "face-names.fixture.json");
    }

    /// <summary>A failure has to NAME the input, and a raw control character in a test message is
    /// invisible exactly where it matters.</summary>
    private static string Printable(string value) =>
        string.Concat(value.Select(c => char.IsControl(c) ? $"\\u{(int)c:x4}" : c.ToString()));

    /// <summary>The theme under test, with one face named — everything else is Photon's.</summary>
    private sealed class FacedTheme(IAppTheme inner, string family) : IAppTheme
    {
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
        public TypeStyle Type(TypeRole role) => inner.Type(role) with { Family = family };
        public ShadowSpec Elevation(int level) => inner.Elevation(level);
        public float Shape(ShapeScale scale) => inner.Shape(scale);
    }
}
